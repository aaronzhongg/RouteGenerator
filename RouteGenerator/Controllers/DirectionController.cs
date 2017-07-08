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

        // TODO: Next step is to take a distance and current location as input, call Google Places API to get some POIs and find a POI that is of the desired (or close to) that
        // of the input distance
        [HttpGet]
        public async Task<IHttpActionResult> GenerateRoute(int inputDistance, String latlng)
        {
            var client = new HttpClient();
            String origin = latlng;
            String destination = null;

            String placesApiPathUrl = "/maps/api/place/nearbysearch/json?location=" + latlng + "&radius=" + inputDistance + "&keyword=park&key=" + googleMapsApiKey; // Search a radius of the inputDistance for POIs

            GoogleDirectionsObject.RootObject googleDirectionObject = null;
            GooglePlacesObject.RootObject googlePlacesObject = null;

            client.BaseAddress = new Uri(googleMapsBaseUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await client.GetAsync(placesApiPathUrl);

            // Get POIs 
            if (response.IsSuccessStatusCode)
            {
                googlePlacesObject = await response.Content.ReadAsAsync<GooglePlacesObject.RootObject>();
            }

            //Query each POI to find one that is close to the user's input distance
            foreach (GooglePlacesObject.Result r in googlePlacesObject.results)
            {
                destination = r.geometry.location.lat + "," + r.geometry.location.lng;
                String directionsApiPathUrl = "/maps/api/directions/json?origin=" + origin + "&destination=" + destination + "&mode=walking&key=" + googleMapsApiKey;
                response = await client.GetAsync(directionsApiPathUrl);

                if (response.IsSuccessStatusCode)
                {
                    googleDirectionObject = await response.Content.ReadAsAsync<GoogleDirectionsObject.RootObject>();

                    foreach (Route route in googleDirectionObject.routes.ToList())
                    {
                        foreach (Leg leg in route.legs.ToList())
                        {

                            // If the route is within 250m of inputDistance then return, otherwise return the one with the closet distance
                            if (Math.Abs(leg.distance.value - inputDistance) < 250)
                            {
                                return Ok(route);
                            }
                        }
                    }
                }
            }

            return Ok(googlePlacesObject);
        }
    }
}
