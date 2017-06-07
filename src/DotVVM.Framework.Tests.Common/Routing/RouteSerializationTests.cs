﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.ResourceManagement;
using DotVVM.Framework.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace DotVVM.Framework.Tests.Common.Routing
{
    [TestClass]
    public class RouteSerializationTests
    {
        [TestMethod]
        public void RouteTable_Deserialization()
        {
            var config1 = DotvvmConfiguration.CreateDefault();
            config1.RouteTable.Add("route1", "url1", "file1.dothtml", new { a = "ccc" });
            config1.RouteTable.Add("route2", "url2/{int:posint}", "file1.dothtml", new { a = "ccc" });

            // Add unknwon constraint, simulate user defined constraint that is not known to the VS Extension
            var r = new DotvvmRoute("url3", "file1.dothtml", new { }, () => null, config1);
            typeof(RouteBase).GetProperty("Url").SetMethod.Invoke(r, new[] { "url3/{a:unsuppotedConstraint}" });
            config1.RouteTable.Add("route3", r);

            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            var config2 = JsonConvert.DeserializeObject<DotvvmConfiguration>(JsonConvert.SerializeObject(config1, settings), settings);

            Assert.AreEqual(config2.RouteTable["route1"].Url, "url1");
            Assert.AreEqual(config2.RouteTable["route2"].Url, "url2/{int:posint}");
            Assert.AreEqual(config2.RouteTable["route3"].Url, "url3/{a:unsuppotedConstraint}");
        }
    }
}
