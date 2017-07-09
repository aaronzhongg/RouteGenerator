using Newtonsoft.Json;
using RouteGenerator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using static RouteGenerator.Models.GoogleDirectionsObject;

namespace RouteGenerator.Controllers
{
    public class DirectionsController : ApiController
    {
        static String googleMapsApiKey = "AIzaSyBUbdzIVx06BmduTI3KNeFuABPziBb6bTY";
        static String googleMapsElevationApiKey = "AIzaSyBF2coZ4mvEw3dkBOkyScnNwydEMrd1gRg";
        String googleMapsBaseUrl = "https://maps.googleapis.com";
        String placesApiPathUrl;
        String directionsApiPathUrl;
        HttpClient client = new HttpClient();

        [HttpGet]
        public async Task<IHttpActionResult> GenerateRoute(int inputDistance, String latlng, int inputElevation = 0)
        {
            String origin = latlng;
            String destination = latlng;

            placesApiPathUrl = SetPlacesApiPathUrl(latlng, inputDistance.ToString()); // Search a radius of the inputDistance for POIs

            GoogleDirectionsObject.RootObject googleDirectionObject = null;
            GooglePlacesObject.RootObject googlePlacesObject = null;

            // Set up HttpClient to call API
            client.BaseAddress = new Uri(googleMapsBaseUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Get POIs 
            HttpResponseMessage response = await client.GetAsync(placesApiPathUrl);
            if (response.IsSuccessStatusCode)
            {
                googlePlacesObject = await response.Content.ReadAsAsync<GooglePlacesObject.RootObject>();
            }

            // Save the return route's distance in separate variable since the total is a sum of an array in the Route object and will be used to find a route close to the input distance
            int returnRouteDistanceDifference = inputDistance; 
            int returnRouteElevationDifference = 10000; // Save elevation for similar reasons to distance
            Route returnRoute = null;

            //Query each POI to find one that is close to the user's input distance
            foreach (GooglePlacesObject.Result r in googlePlacesObject.results)
            {
                // Using the returned POI as a waypoints
                String waypoints = r.geometry.location.lat + "," + r.geometry.location.lng;
                directionsApiPathUrl = SetDirectionsApiPathUrl(origin, destination, waypoints);
                response = await client.GetAsync(directionsApiPathUrl);

                if (response.IsSuccessStatusCode)
                {
                    googleDirectionObject = await response.Content.ReadAsAsync<GoogleDirectionsObject.RootObject>();

                    foreach (Route route in googleDirectionObject.routes.ToList())
                    {
                        // Calculate the total distance of the route
                        int currentRouteTotalDistance = 0;
                        foreach (Leg leg in route.legs.ToList())
                        {
                            currentRouteTotalDistance += leg.distance.value;
                        }

                        // Check the elevation of the route 
                        int currentRouteElevationDifference = Math.Abs(await CheckElevationAsync(route.overview_polyline.points, currentRouteTotalDistance) - inputElevation);
                        int currentRouteDistanceDifference = Math.Abs(currentRouteTotalDistance - inputDistance);

                        // If the route is within 500m of inputDistance then return AND elevation difference is within 200m, otherwise return the one with the closet distance
                        // TODO: some better algorithm for choosing a route based on distance and elevation
                        if (currentRouteDistanceDifference < 500 &  currentRouteElevationDifference < 200)
                        {
                            return Ok(route);
                        }
                        else
                        {
                            // Save the route which has the closest distance to input distance
                            if (currentRouteDistanceDifference < returnRouteDistanceDifference)
                            {
                                returnRouteElevationDifference = currentRouteElevationDifference;
                                returnRouteDistanceDifference = currentRouteDistanceDifference;
                                returnRoute = route;
                            }
                        }
                    }
                }
            }

            return Ok(returnRoute);
        }

        private async Task<int> CheckElevationAsync(String path, int routeDistance)
        {
            // Take the elevation for every 200m of the route
            int numberOfPoints = routeDistance / 200;

            String elevationApiPathUrl = "/maps/api/elevation/json?path=enc:" + path + "&samples=" + numberOfPoints + "&key=" + googleMapsElevationApiKey;

            HttpResponseMessage response = await client.GetAsync(elevationApiPathUrl);

            GoogleElevationObject.RootObject googleElevationObject = null;

            if (response.IsSuccessStatusCode)
            {
                googleElevationObject = await response.Content.ReadAsAsync<GoogleElevationObject.RootObject>();
            }

            double totalElevation = 0;
            double prevPointElevation = googleElevationObject.results.ElementAt(0).elevation;
            for (int i = 1; i < googleElevationObject.results.Count; i++) 
            {
                GoogleElevationObject.Result r = googleElevationObject.results.ElementAt(i);

                // Ignore the downhill sections 
                if (prevPointElevation < r.elevation)
                {
                    totalElevation += r.elevation;
                }
               
            }

            return (int)totalElevation;
        }

        private String SetDirectionsApiPathUrl(String origin, String destination, String waypoints)
        {
            return "/maps/api/directions/json?origin=" + origin + "&destination=" + destination + "&mode=walking&waypoints=" + waypoints + "&key=" + googleMapsApiKey;
        }

        private String SetPlacesApiPathUrl(String latlng, String radius)
        {
            return "/maps/api/place/nearbysearch/json?location=" + latlng + "&radius=" + radius + "&type=park&key=" + googleMapsApiKey;
        }

    }
}
