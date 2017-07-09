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
        String googleMapsBaseUrl = "https://maps.googleapis.com";
        String placesApiPathUrl;
        String directionsApiPathUrl;

        [HttpGet]
        public async Task<IHttpActionResult> GenerateRoute(int inputDistance, String latlng)
        {
            String origin = latlng;
            String destination = latlng;

            placesApiPathUrl = SetPlacesApiPathUrl(latlng, inputDistance.ToString()); // Search a radius of the inputDistance for POIs

            GoogleDirectionsObject.RootObject googleDirectionObject = null;
            GooglePlacesObject.RootObject googlePlacesObject = null;

            // Set up HttpClient to call API
            var client = new HttpClient();
            client.BaseAddress = new Uri(googleMapsBaseUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Get POIs 
            HttpResponseMessage response = await client.GetAsync(placesApiPathUrl);
            if (response.IsSuccessStatusCode)
            {
                googlePlacesObject = await response.Content.ReadAsAsync<GooglePlacesObject.RootObject>();
            }

            Route returnRoute = null;
            int returnRouteDistanceDifference = inputDistance; // Save the return route's distance in separate variable since the total is a sum of an array in the Route object

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

                        // If the route is within 200m of inputDistance then return, otherwise return the one with the closet distance
                        if (Math.Abs(currentRouteTotalDistance - inputDistance) < 200)
                        {
                            return Ok(route);
                        }
                        else
                        {
                            int currentRouteDistanceDifference = Math.Abs(currentRouteTotalDistance - inputDistance);
                            // Save the route which has the closest distance to input distance
                            if (currentRouteDistanceDifference < returnRouteDistanceDifference)
                            {
                                returnRouteDistanceDifference = currentRouteDistanceDifference;
                                returnRoute = route;
                            }
                        }
                    }
                }
            }

            return Ok(returnRoute);
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
