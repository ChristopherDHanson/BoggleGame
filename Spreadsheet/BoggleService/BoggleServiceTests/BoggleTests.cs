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
        public void TestMethod2()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response r = client.DoPostAsync("users", users).Result;
            Assert.AreEqual(Created, r.Status);
        }

        [TestMethod]
        public void TestMethod3()
        {

        }

        [TestMethod]
        public void TestMethod4()
        {

        }

        [TestMethod]
        public void TestMethod5()
        {

        }

        [TestMethod]
        public void TestMethod6()
        {

        }

        [TestMethod]
        public void TestMethod7()
        {

        }

        [TestMethod]
        public void TestMethod8()
        {

        }

        [TestMethod]
        public void TestMethod9()
        {

        }

        [TestMethod]
        public void TestMethod10()
        {

        }

        [TestMethod]
        public void TestMethod11()
        {

        }

        [TestMethod]
        public void TestMethod12()
        {

        }

        [TestMethod]
        public void TestMethod13()
        {

        }

        [TestMethod]
        public void TestMethod14()
        {

        }

        [TestMethod]
        public void TestMethod15()
        {

        }

        [TestMethod]
        public void TestMethod16()
        {

        }

        [TestMethod]
        public void TestMethod17()
        {

        }

        [TestMethod]
        public void TestMethod18()
        {

        }

        [TestMethod]
        public void TestMethod19()
        {

        }

        [TestMethod]
        public void TestMethod20()
        {

        }
    }
}
