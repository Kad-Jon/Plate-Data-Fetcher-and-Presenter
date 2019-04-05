# Plate-Data-Fetcher-and-Presenter
This WPF application recognises and saves license plates from a video stream, scrapes a web page for data associated with that plate and
presents the results to the user. 

Background:

I'm currently developing this project as part of a larger business application for potential use in a car detailing business. The purpose 
of the larger application is to identify cars on premises, obtain vehicle data (time of entry, make, model) and push these onto to a
database. A mobile application will observe the database for vehicles and allow employees to push further business data regarding the 
vehicle onto the database if a service is bought. This data can be used on the fly to order operations (queue ordering algorithms)
and identify blacklisted vehicles but more importantly will allow for easier large scale data acquisition and analysis. 

This Application:

This program is to test the functionality of the initial phase of the application, namely the plate recognition and data acquisition phase.
For plate recognition I've used the open source library openalpr to return a list of plate results from an image. WPF has been used to 
build an an intuitive GUI so a non computer literate user can configure the capture and recognition objects as required, without too much
training. To obtain basic vehicle data a free service on a reg lookup webpage (mycarcheck.com) is scraped. This, together with a regular
expression, acts also to filter invalid and innaccurate results returned by the recognition object. 

Currently this program has only been tested (succesfully) using a laptop webcam and I am waiting on acquistion of a decent quality IP camera
to test this further.
