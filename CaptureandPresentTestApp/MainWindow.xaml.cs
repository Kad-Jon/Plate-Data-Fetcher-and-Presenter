using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using openalprnet;
using Emgu.CV;
using DirectShowLib;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using System.Net;
using System.Data.Sql;
using System.Data.SqlTypes;

namespace CaptureandPresentTestApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string directory = Directory.GetCurrentDirectory();

        /* Reference to alpr, video capture, timer and timer callback objects. The main process of this application is managed by these objects. The videocapture object provides a video stream from a camera of choice. 
         The timer periodically calls a function which calls the alpr object recognize method. The recognize method grabs a frame from the video stream and returns any plate results */
        public AlprNet alpr;
        public VideoCapture capture;
        public Timer timer;
        public TimerCallback callback;

        // Variables which store WPF Control values. Are set to default values on opening and are changed by the appropriate event handlers
        public double confidenceThreshold;
        public bool gbRegExEnabled;
        public int recFrequency;
        public int TopN;

        // Regular Expression which the user can decide to use to filter out plate results that don't conform to the gb license plate format
        public string validationExpression = @"(?<Current>^[A-Z]{2}[0-9]{2}[A-Z]{3}$)|(?<Prefix>^[A-Z][0-9]{1,3}[A-Z]{3}$)|(?<Suffix>^[A-Z]{3}[0-9]{1,3}[A-Z]$)|(?<DatelessLongNumberPrefix>^[0-9]{1,4}[A-Z]{1,2}$)|"
        + "(?<DatelessShortNumberPrefix>^[0-9]{1,3}[A-Z]{1,3}$)|(?<DatelessLongNumberSuffix>^[A-Z]{1,2}[0-9]{1,4}$)|(?<DatelessShortNumberSufix>^[A-Z]{1,3}[0-9]{1,3}$)";

        // Test camera app address
        public string testCameraStreamAdress = "rtsp://192.168.0.24";

        // List to store any uniquely recognized plates
        public static List<string> livePlates;

        // Lists to store car objects which are instatiated using plates in Liveplates and are first stored in unVerifiedCars. The regLookup method verifies the car and verified cars are placed in the verifiedCars List. 
        public static List<Car> unVerifiedCars;
        public static List<Car> verifiedCars;
        
        // A list of car description strings constructed from car objects in verifiedCars. The is set as the ListBox data source to display valid results to user.
        public List<string> ListBoxItems;

        // Entry point for program
        public MainWindow()
        {
            InitializeComponent();

            // Insantiate List objects
            livePlates = new List<String>();
            unVerifiedCars = new List<Car>();
            verifiedCars = new List<Car>();
            ListBoxItems = new List<string>();

            // Prepare UI elements as appropriate.
            EndCaptureButton.IsEnabled = false;
            gbRegExEnabled = (bool)ValidatorCheckBox.IsChecked;
            ConfidenceLabel.Content = "Confidence: " + ConfidenceSlider.Value.ToString() + "%";
            TopN = (int)TopNSlider.Value;
            TopNLabel.Content = "Top N: " + TopN.ToString();
            confidenceThreshold = ConfidenceSlider.Value;
            recFrequency = (int)RecFreqSlider.Value;
            RecFreqLabel.Content = "Recognition Frequency: " + recFrequency.ToString() + "Hz";

        }

        // Function to initialize alpr object as configured
        private void initOpenAlpr()
        {
            alpr = new AlprNet("eu", directory + "\\openalpr_64\\openalpr.conf", directory + "\\openalpr_64\\runtime_data");
            alpr.TopN = TopN;
        }

        // Function to initialize videocapture object, adds an event handler(to handle image display) to any image grabbed event and begins capture
        private void initVideoCapture(int index)
        {
            if (index == 0)
            {
                capture = new VideoCapture();
                capture.ImageGrabbed += Capture_ImageGrabbed;
                capture.Start();
            } else if (index == 1)
            {
                capture = new VideoCapture(testCameraStreamAdress);
                capture.ImageGrabbed += Capture_ImageGrabbed;
                capture.Start();
            }
        }

        // Event handler which displays any frame grabbed to the user. Function from external library bitmapSource is used to convert Mat object into an imageSource object which can be used as a source for the WPF Image Control
        private void Capture_ImageGrabbed(object sender, EventArgs e)
        {
            Mat m = new Mat();
            try {
                capture.Retrieve(m);
                var imageSource = bitmapSource(m);
                imageSource.Freeze();
                CameraCaptureBox.Dispatcher.Invoke(() =>
                {
                    CameraCaptureBox.Source = imageSource;
                });
            } catch (NullReferenceException ex)
            {
                Console.WriteLine("NullReferenceException in Capture_ImageGrabbed Event Handler");
            } catch (ArgumentNullException ex)
            {
                Console.WriteLine("ArgumentNullException in Capture_ImageGrabbed Event Handler");
            } catch (System.AccessViolationException ex)
            {
                Console.WriteLine("AccessViolationException in Capture_ImageGrabbed Event Handler");
            } finally
            {
                m.Dispose();
            }
        }

        // Instatiate timer object as configured
        private void initTimer(int freq)
        {
            callback = new TimerCallback(Tick);
            var period = 1000 / freq;
            timer = new Timer(callback, null, 0, period);
        }

        // Callback function declaration. Plate recognition is attemted when called by timer
        private void Tick(Object stateInfo)
        {
            processImage();
        }

        // This method attempts to recognize plates periodically and sends them off for further processing
        private void processImage()
        {
            Mat m = new Mat();
            try
            {
                capture.Retrieve(m);
                var results = alpr.Recognize(m.Bitmap);
                foreach (var result in results.Plates)
                {
                    foreach (var plate in result.TopNPlates)
                    {
                        if (gbRegExEnabled == true)
                        {
                            if (plate.OverallConfidence > confidenceThreshold && !livePlates.Contains(plate.Characters) && Regex.IsMatch(plate.Characters, validationExpression))
                            {
                                livePlates.Add(plate.Characters);
                                Console.WriteLine(plate.Characters + "  " + livePlates.Count);
                                processPlate(plate.Characters);
                                
                            }
                        } else if (gbRegExEnabled == false)
                        {
                            if (plate.OverallConfidence > confidenceThreshold && !livePlates.Contains(plate.Characters))
                            {
                                livePlates.Add(plate.Characters);
                                Console.WriteLine(plate.Characters + "  " + livePlates.Count);
                                processPlate(plate.Characters);
                            }
                        }
                    }
                }

            } catch (NullReferenceException ex)
            {
                Console.WriteLine("Null Reference Exception in processImage method.");
            } catch (ArgumentNullException ex)
            {
                Console.WriteLine("ArgumentNullException in processImage method");
            } catch (System.AccessViolationException ex)
            {
                Console.WriteLine("AccessViolationException in processImage method");
            } finally
            {
                m.Dispose();
            }

            verifyPlates();
            populateListView();
        }

        // Instatiates a car object using recognized plate and accounts for common mistakes by instantiating any plates for which it may have been mistaken
        private void processPlate(string regNum)
        {
            // Instantiate a car object using plate and time and add to unverified cars list, then attempt regLookup on those cars
            Car car = new Car(regNum, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            unVerifiedCars.Add(car);
            car.RegLookup();

            // Account for recognition mistakes between charcters '1' and 'I' and characters '0' and 'O'. Can return many results but regLookup filters many of these out
            if (regNum.Contains("1")||regNum.Contains("I"))
            {
                produceRegNumCorrections(regNum, '1', 'I');
            }

            if (regNum.Contains("0") || regNum.Contains("O"))
            {
                produceRegNumCorrections(regNum, '1', 'I');
            }
        }

        // Method which sifts through unVerifiedCars for any cars which are verified and adds them to verifiedCars and removes them from unVerifiedCars
        public void verifyPlates()
        {
            foreach (Car car in unVerifiedCars.ToList())
            {
                if (car.isVerified == true)
                {
                    verifiedCars.Add(car);
                    unVerifiedCars.Remove(car);
                }
            }
        }

        // MARK: - Correction Algorithms 

        // Function which takes in a string and two characters which are commonly mistaken for one another by the alpr. This function attempts to account for these mistakes and manufactures all combinations of corrections
        public static void produceRegNumCorrections(string regNum, char charOne, char charTwo)
        {
            // primes candidate characters for use in alogorithm by setting them all to same value
            string equalizedRegNum = EqualizeChars(regNum, charOne, charTwo);

            // Produces all combinations using primed string
            produceCombinations(equalizedRegNum, charOne, charTwo);
        }

        public static string EqualizeChars(string regNum, char target, char replacement)
        {
            char[] regNumAsChars = regNum.ToCharArray();

            int i = 0;

            foreach (char c in regNum)
            {
                if (c == target || c == replacement)
                {
                    regNumAsChars[i] = target;
                }

                i++;
            }

            return new string(regNumAsChars);
        }

        public static void produceCombinations(string regNum, char target, char replacement)
        {
            char[] regNumAsChars = regNum.ToCharArray();
            
            int i = 0;

            foreach(char c in regNumAsChars)
            {
                if (c == target)
                {
                    char[] newRegNumAsChars = new char[regNumAsChars.Length];
                    Array.Copy(regNumAsChars, 0, newRegNumAsChars, 0, newRegNumAsChars.Length);
                    newRegNumAsChars[i] = replacement;
                    string newRegNum = new string(newRegNumAsChars);
                    livePlates.Add(newRegNum);
                    Console.WriteLine(newRegNum + "  " + livePlates.Count + " is corrected plate");


                    var car = new Car(regNum, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    if (!unVerifiedCars.Contains(car))
                    {
                        unVerifiedCars.Add(car);
                        car.isCorrected = true;
                        car.RegLookup();
                    }

                    produceCombinations(newRegNum, target, replacement);

                    
                }
                i++;
            }
        }

        // Imports DLL for use in function which converts an image into a bitmap Source
        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        public static BitmapSource bitmapSource(IImage image)
        {
            using (Bitmap source = image.Bitmap)
            {               
                    IntPtr ptr = source.GetHbitmap();
                
                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        ptr,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(ptr); //release the HBitmap
                return bs;
            }
        }

        // Constructs a car description for each car in verified cars and adds it to listBoxItems which is set as the data source for the Confident_Plates Listbox.
        private void populateListView()
        {
            Confident_Plates.Dispatcher.Invoke(() =>
            {

                foreach (Car car in verifiedCars)
                {
                    string carDescription = car.regNumber + "      " + car.makeModel;

                    if (!ListBoxItems.Contains(carDescription))
                    {
                        ListBoxItems.Add(carDescription);
                    }
                }

                Confident_Plates.ItemsSource = null;
                Confident_Plates.ItemsSource = ListBoxItems;
                Confident_Plates.Items.Refresh();
            
            });

        }

        // MARK: - UI Event Handlers

        // Button begins capture and recognition
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Checks which camera stream to capture
            if (cameracombobox.SelectedIndex == 0)
            {
                initOpenAlpr();
                initVideoCapture(0);
                initTimer(recFrequency);
            }
            else if (cameracombobox.SelectedIndex == 1)
            {
                initOpenAlpr();
                initVideoCapture(1);
                initTimer(recFrequency);
            }

            // Disable user controls as appropriate 
            StartCaptureButton.IsEnabled = false;
            cameracombobox.IsEnabled = false;
            EndCaptureButton.IsEnabled = true;
            ConfidenceSlider.IsEnabled = false;
            TopNSlider.IsEnabled = false;
            RecFreqSlider.IsEnabled = false;
            ValidatorCheckBox.IsEnabled = false;
        }

        // Event handler for end capture button click
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Thread.Sleep(300);
            // Dispose of all recognition, capture and timer objects            
            timer.Dispose();
            capture.ImageGrabbed -= Capture_ImageGrabbed;
            capture.Stop();
            capture.Dispose();
            
            // Enable and disable appropriate UI elements
            StartCaptureButton.IsEnabled = true;
            cameracombobox.IsEnabled = true;
            CameraCaptureBox.Source = null;
            EndCaptureButton.IsEnabled = false;
            ConfidenceSlider.IsEnabled = true;
            TopNSlider.IsEnabled = true;
            RecFreqSlider.IsEnabled = true;
            ValidatorCheckBox.IsEnabled = true;
        }

        // MARK: -  Event Handlers for user controls which set various recognition properties

        private void ConfidenceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            confidenceThreshold = ConfidenceSlider.Value;

            try
            {
                ConfidenceSlider.Dispatcher.Invoke(() =>
                {
                    ConfidenceLabel.Content = "Confidence: " + ConfidenceSlider.Value.ToString("N1") + "%";
                });

            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine("NullReferenceException in ConfidenceSlider_ValueChanged event handler");
            }           
        }

        private void TopNSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TopN = (int)TopNSlider.Value;

            try
            {
                TopNSlider.Dispatcher.Invoke(() =>
                {
                    TopNLabel.Content = "Top N: " + TopN.ToString();
                });
            } catch (NullReferenceException ex)
            {
                Console.WriteLine("NullReferenceException in TopNSlide_ValueChanged event handler");
            }
        }

        private void RecFreqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            recFrequency = (int)RecFreqSlider.Value;

            try
            {
                RecFreqSlider.Dispatcher.Invoke(() =>
                {
                    RecFreqLabel.Content = "Recognition Frequency: " + recFrequency.ToString() + "Hz";
                });
            } catch (NullReferenceException ex)
            {
                Console.WriteLine("NullReferenceException in RecFreqSlider_ValueChanged event handler");
            }
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void Cameracombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void ValidatorCheckBox_Checked(object sender, RoutedEventArgs e)
        {          
            gbRegExEnabled = true;
        }

        private void ValidatorCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            gbRegExEnabled = false;
        }
       
    }

    /* Car object class declaration. Many of the declared properties are to be implemented in the future*/

    public class Car
    {
        public bool isCorrected = false;

        // Properties set on recognition
        public string timeOfRecognition;
        public string regNumber;

        // Properties set on reg lookup
        public bool isVerified = false;
        public string makeModel;
        public string colour;
        public string bodyType;
        public string year;

        //  Properties to be set in the iPhone App
        public string size;
        public string service;
        public int waitingTime;
        public DateTime timeOfOrder;
        public string preNotes;

        // Properties set on completion of service
        public DateTime timeOfCompletion;

        // Properties set on completion of transaction
        public int payment;
        public bool isDiscounted;
        public int discount;
        public string postNotes;

        public Car(string regNumber, string timeOfRecognition)
        {
            this.timeOfRecognition = timeOfRecognition;
            this.regNumber = regNumber;
        }

        // Method which scrapes the mycarcheck.com webpage for presence in vehicle registry database and loads data which the check retrieves into the appropriate properties
        public void RegLookup()
        {
            if (isVerified == false)
            {
                HtmlWeb web = new HtmlWeb();
                HtmlDocument document = web.Load("https://www.mycarcheck.com/check/" + regNumber);

                if (document.GetElementbyId("application").GetAttributeValue("class", "") != "404")
                {
                    isVerified = true;
                    HtmlNode makeNode = document.DocumentNode.SelectSingleNode("//*[@id=\"application\"]/main/div[3]/div[2]/div[1]/span");
                    HtmlNode bodyStyleNode = document.DocumentNode.SelectSingleNode("//*[@id=\"application\"]/main/div[3]/div[2]/div[2]/div[2]/span[1]");
                    HtmlNode colourNode = document.DocumentNode.SelectSingleNode("//*[@id=\"application\"]/main/div[3]/div[2]/div[2]/div[2]/span[2]");
                    HtmlNode yearNode = document.DocumentNode.SelectSingleNode("//*[@id=\"application\"]/main/div[3]/div[2]/div[2]/div[2]/span[4]");
                    makeModel = makeNode.InnerHtml;
                    bodyType = bodyStyleNode.InnerHtml;
                    colour = colourNode.InnerHtml;
                    year = yearNode.InnerHtml;
                }
                else
                {
                    Console.WriteLine("404");
                }
            }
        }
    }
}
