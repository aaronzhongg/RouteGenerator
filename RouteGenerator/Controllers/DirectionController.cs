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

namespace RouteGenerator.Controllers
{
    public class DirectionController : ApiController
    {
        String directionsApiBaseUrl = "https://maps.googleapis.com";
        String directionsApiPathUrl = "/maps/api/directions/json?origin=Pakuranga,nz&destination=Botany,nz&mode=walking&key=AIzaSyBUbdzIVx06BmduTI3KNeFuABPziBb6bTY";

        [HttpGet]
        public async Task<IHttpActionResult> GenerateRoute()
        {
            GoogleDirectionObject.RootObject googleDirectionObject = null;
            var client = new HttpClient();
            client.BaseAddress = new Uri(directionsApiBaseUrl);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await client.GetAsync(directionsApiPathUrl);

            if (response.IsSuccessStatusCode)
            {
                googleDirectionObject = await response.Content.ReadAsAsync<GoogleDirectionObject.RootObject>();
            }

            return Ok(googleDirectionObject);
        }
    }
}
