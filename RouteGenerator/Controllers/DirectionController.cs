using Newtonsoft.Json;
using RouteGenerator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using static RouteGenerator.Models.GoogleDirectionsObject;
using static RouteGenerator.Models.GooglePlacesObject;

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
        public async Task<IHttpActionResult> GenerateRoute(double inputDistance, String latlng, double inputElevation = 0.00)
        {
            String origin = latlng;
            String destination = latlng;

            placesApiPathUrl = SetPlacesApiPathUrl(latlng, (inputDistance/4).ToString()); // Search a radius of half the inputDistance for POI to prevent fetching too many

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
            
            //If leass than 2 POIs were found, increase the search radius
            if (googlePlacesObject.results.Count < 2)
            {
                placesApiPathUrl = SetPlacesApiPathUrl(latlng, (inputDistance/2).ToString());
                response = await client.GetAsync(placesApiPathUrl);
                if (response.IsSuccessStatusCode)
                {
                    googlePlacesObject = await response.Content.ReadAsAsync<GooglePlacesObject.RootObject>();
                }
            }
            Random rnd = new Random();
            //Limit the number of POIs to 10 to prevent long waits
            while (googlePlacesObject.results.Count > 15)
            {
                googlePlacesObject.results.RemoveAt(rnd.Next((googlePlacesObject.results.Count)));
            }

            // Save the return route's distance in separate variable since the total is a sum of an array in the Route object and will be used to find a route close to the input distance
            double returnRouteDistanceDifference = inputDistance;
            double returnRouteElevationDifference = 10000; // Save elevation for similar reasons to distance
            double returnRouteIdealness = 10000;
            double returnRouteDistance = 0;
            double returnRouteElevation = 0;
            List<RouteDTO> possibleRoutes = new List<RouteDTO>();
            
            Route returnRoute = null;

            //Query each POI to find one that is close to the user's input distance
            //foreach (GooglePlacesObject.Result r in googlePlacesObject.results)
            for (int i = googlePlacesObject.results.Count - 1; i >= 0; i--)
            {
                Result firstResult = googlePlacesObject.results.ElementAt(i);
                String firstWaypoint = firstResult.geometry.location.lat + "," + firstResult.geometry.location.lng;
                for (int j = i; j >= 0; j--)
                {
                    Result secondResult = googlePlacesObject.results.ElementAt(j); 
                    // Using the returned POI as a waypoints
                    String secondWaypoint = secondResult.geometry.location.lat + "," + secondResult.geometry.location.lng;

                    String waypoints = firstWaypoint + "|" + secondWaypoint;
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
                            double currentRouteElevation = await CheckElevationAsync(route.overview_polyline.points, currentRouteTotalDistance);
                            double currentRouteElevationDifference = Math.Abs(100 * (currentRouteElevation - inputElevation)/inputElevation);
                            double currentRouteDistanceDifference = Math.Abs(100 * (currentRouteTotalDistance - inputDistance)/inputDistance);

                            double currentRouteIdealness = CalculateRouteIdealness(currentRouteDistanceDifference, currentRouteElevationDifference);

                            if (currentRouteIdealness < 10)     //Overall at least 80% ideal
                            {
                                //return Ok(ProcessReturnObject(route, currentRouteTotalDistance, currentRouteElevation));
                                possibleRoutes.Add(ProcessReturnObject(route, currentRouteTotalDistance, currentRouteElevation));
                                if(possibleRoutes.Count >= 5)
                                {
                                    //Return a random route if there are 5 or more viable routes
                                    int index = rnd.Next(possibleRoutes.Count);
                                    return Ok(possibleRoutes[index]);
                                }
                            }
                            else
                            {
                                if (currentRouteIdealness < returnRouteIdealness)
                                {
                                    returnRouteDistance = currentRouteTotalDistance;
                                    returnRouteElevation = currentRouteElevation;
                                    returnRouteElevationDifference = currentRouteElevationDifference;
                                    returnRouteDistanceDifference = currentRouteDistanceDifference;
                                    returnRouteIdealness = currentRouteIdealness;
                                    returnRoute = route;

                                }
                            }
                        }
                    }
                }
            }
            // Return a random route from the possible list of routes
            if (possibleRoutes.Count > 0)
            {
                int index = rnd.Next(possibleRoutes.Count);
                return Ok(possibleRoutes[index]);
            }
            // Otherwise Return best route available 
            return Ok(ProcessReturnObject(returnRoute, returnRouteDistance, returnRouteElevation));
        }

        private async Task<double> CheckElevationAsync(String path, double routeDistance)
        {
            // Take the elevation for every 200m of the route
            int numberOfPoints = (int)routeDistance / 200;

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

                prevPointElevation = r.elevation;
            }

            return Math.Round(totalElevation,2);
        }

        // Calculates the idealness of a route considering distance difference between route and input distance is of 70% importance and elevation difference is 30%
        private double CalculateRouteIdealness(double distanceDifference, double elevationDifference)
        {
            return (0.8 * distanceDifference) + (0.2 * elevationDifference);
        }


        private String SetDirectionsApiPathUrl(String origin, String destination, String waypoints)
        {
            return "/maps/api/directions/json?origin=" + origin + "&destination=" + destination + "&mode=walking&waypoints=" + waypoints + "&key=" + googleMapsApiKey;
        }

        private String SetPlacesApiPathUrl(String latlng, String radius)
        {
            return "/maps/api/place/nearbysearch/json?location=" + latlng + "&radius=" + radius + "&type=park&key=" + googleMapsApiKey;
        }

        private RouteDTO ProcessReturnObject(Route route, double distance, double elevation)
        {
            var dto = new RouteDTO();
            var points = new List<Coordinate>();
            Coordinate c;

            foreach (Leg leg in route.legs)
            {
                foreach (Step step in leg.steps)
                {
                    c = new Coordinate()
                    {
                        lat = step.start_location.lat,
                        lng = step.start_location.lng,
                        instruction = ProcessInstruction(step.html_instructions)
                    };
                    points.Add(c);
                }
            }

            var endPoint = route.legs.Last().steps.Last();

            //c = new Coordinate()
            //{
            //    lat = endPoint.end_location.lat,
            //    lng = endPoint.end_location.lng,
            //    instruction = ProcessInstruction(endPoint.html_instructions)
            //};
            //points.Add(c);

            dto.points = points;
            dto.distance = distance;
            dto.elevation = elevation;

            return dto;
        }

        private String ProcessInstruction(String instruction)
        {
            instruction = Regex.Replace(instruction, @" ?\</.*?\>", "");
            return Regex.Replace(instruction, @" ?\<.*?\>", " ");
        }

    }


}
