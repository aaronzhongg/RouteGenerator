using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RouteGenerator.Models
{
    public class RouteDTO
    {
        public List<Coordinate> points { get; set; }
        public double distance { get; set; }
        public double elevation { get; set; }
    }

    public class Coordinate
    {
        public String instruction { get; set; }
        public double lat { get; set; }
        public double lng { get; set; }
    }
}