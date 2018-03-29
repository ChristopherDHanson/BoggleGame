using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static System.Net.HttpStatusCode;
using System.Diagnostics;
using System.Dynamic;
using Newtonsoft.Json;

namespace Boggle
{
    /// <summary>
    /// Provides a way to start and stop the IIS web server from within the test
    /// cases.  If something prevents the test cases from stopping the web server,
    /// subsequent tests may not work properly until the stray process is killed
    /// manually.
    /// </summary>
    public static class IISAgent
    {
        // Reference to the running process
        private static Process process = null;

        /// <summary>
        /// Starts IIS
        /// </summary>
        public static void Start(string arguments)
        {
            if (process == null)
            {
                ProcessStartInfo info = new ProcessStartInfo(Properties.Resources.IIS_EXECUTABLE, arguments);
                info.WindowStyle = ProcessWindowStyle.Minimized;
                info.UseShellExecute = false;
                process = Process.Start(info);
            }
        }

        /// <summary>
        ///  Stops IIS
        /// </summary>
        public static void Stop()
        {
            if (process != null)
            {
                process.Kill();
            }
        }
    }
    [TestClass]
    public class BoggleTests
    {
        /// <summary>
        /// This is automatically run prior to all the tests to start the server
        /// </summary>
        [ClassInitialize()]
        public static void StartIIS(TestContext testContext)
        {
            IISAgent.Start(@"/site:""BoggleService"" /apppool:""Clr4IntegratedAppPool"" /config:""..\..\..\.vs\config\applicationhost.config""");
        }

        /// <summary>
        /// This is automatically run when all tests have completed to stop the server
        /// </summary>
        [ClassCleanup()]
        public static void StopIIS()
        {
            IISAgent.Stop();
        }

        private RestTestClient client = new RestTestClient("http://localhost:60000/BoggleService.svc/");

        /// <summary>
        /// Note that DoGetAsync (and the other similar methods) returns a Response object, which contains
        /// the response Stats and the deserialized JSON response (if any).  See RestTestClient.cs
        /// for details.
        /// </summary>
        [TestMethod]
        public void TestMethod1()
        {
            Response r = client.DoGetAsync("word?index={0}", "-5").Result;
            Assert.AreEqual(Forbidden, r.Status);

            r = client.DoGetAsync("word?index={0}", "5").Result;
            Assert.AreEqual(OK, r.Status);

            string word = (string) r.Data;
            Assert.AreEqual("AAL", word);
        }

        [TestMethod]
        public void TestBasicRegister()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response r = client.DoPostAsync("users", users).Result;
            Assert.AreEqual(Created, r.Status);
        }

        [TestMethod]
        public void TestRegisterLongName()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Llanfairpwllgwyngyllgogerychwyrndrobwllllantysiliogogogoch";
            Response r = client.DoPostAsync("users", users).Result;
            Assert.AreEqual(Forbidden, r.Status);
        }

        [TestMethod]
        public void TestRegisterEmptyName()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "";
            Response r = client.DoPostAsync("users", users).Result;
            Assert.AreEqual(Forbidden, r.Status);
        }

        [TestMethod]
        public void TestReturnedUserToken()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Bilbo";
            Response r = client.DoPostAsync("users", users).Result;
            string tokenReturned = r.Data.ToString();
            Console.WriteLine(tokenReturned);
            if (tokenReturned.Length < 3)
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public void TestMethod6()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 121;

            dynamic users2 = new ExpandoObject();
            users.Nickname = "Joe";
            Response s = client.DoPostAsync("users", users).Result;
            users2.UserToken = s.Data.UserToken;
            users2.TimeLimit = 121;

            Response r = client.DoPostAsync("games", users).Result;
            Assert.AreEqual(Forbidden, r.Status);

        }

        [TestMethod]
        public void TestMethod7()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestMethod8()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestMethod9()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestMethod10()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestMethod11()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestMethod12()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestMethod13()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestMethod14()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestMethod15()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestMethod16()
        {
            Assert.Fail();
        }
    }
}
