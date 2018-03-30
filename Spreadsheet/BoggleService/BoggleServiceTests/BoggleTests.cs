using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static System.Net.HttpStatusCode;
using System.Diagnostics;
using System.Dynamic;
using System.Threading;
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

        [TestMethod]
        public void TestPendingStatus()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;

            dynamic userInfo = new ExpandoObject();
            userInfo.UserToken = q.Data.UserToken;
            userInfo.TimeLimit = 120;
            Response k = client.DoPostAsync("games", userInfo).Result;
            int gameID = k.Data.GameID;
            Response t = client.DoGetAsync("games/"+gameID, "").Result;
            Assert.AreEqual("pending", t.Data.GameState.ToString());
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
        public void RegisterLongName()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Llanfairpwllgwyngyllgogerychwyrndrobwllllantysiliogogogoch";
            Response r = client.DoPostAsync("users", users).Result;
            Assert.AreEqual(Forbidden, r.Status);
        }

        [TestMethod]
        public void RegisterEmptyName()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "";
            Response r = client.DoPostAsync("users", users).Result;
            Assert.AreEqual(Forbidden, r.Status);
        }

        [TestMethod]
        public void ReturnedUserToken()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Bilbo";
            Response r = client.DoPostAsync("users", users).Result;
            string tokenReturned = r.Data.ToString();
            if (tokenReturned.Length < 3)
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public void RegisterTestNullNickname()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = null;
            Response r = client.DoPostAsync("users", users).Result;
            Assert.AreEqual(Forbidden, r.Status);
        }

        [TestMethod]
        public void JoinInvalidTimeLimit121()
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
        public void JoinAsFirstPlayerExpectAccepted()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 80;

            Response r = client.DoPostAsync("games", users).Result;
            users = new ExpandoObject();
            q = client.DoPutAsync(users, "games").Result;
            Assert.AreEqual(Accepted, r.Status);
        }

        [TestMethod]
        public void JoinAsFirstPlayerAlreadyPendingExpectConflict()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 80;

            Response r = client.DoPostAsync("games", users).Result;
            r = client.DoPostAsync("games", users).Result;
            r = client.DoPostAsync("games", users).Result;
            Assert.AreEqual(Conflict, r.Status);
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
        public void TestCompleteGameState()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;

            dynamic userInfo = new ExpandoObject();
            userInfo.UserToken = q.Data.UserToken;
            userInfo.TimeLimit = 5;

            dynamic users2 = new ExpandoObject();
            users2.Nickname = "Joe";
            Response q2 = client.DoPostAsync("users", users).Result;

            dynamic userInfo2 = new ExpandoObject();
            userInfo2.UserToken = q2.Data.UserToken;
            userInfo2.TimeLimit = 5;


            Response k = client.DoPostAsync("games", userInfo).Result;
            Response l = client.DoPostAsync("games", userInfo2).Result;
            int gameID = k.Data.GameID;

            Thread.Sleep(7000);

            Response t = client.DoGetAsync("games/" + gameID, "").Result;
            Assert.AreEqual("completed", t.Data.GameState.ToString());
        }

        [TestMethod]
        public void TestActiveGameState()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;

            dynamic userInfo = new ExpandoObject();
            userInfo.UserToken = q.Data.UserToken;
            userInfo.TimeLimit = 120;

            dynamic users2 = new ExpandoObject();
            users2.Nickname = "Joe";
            Response q2 = client.DoPostAsync("users", users).Result;

            dynamic userInfo2 = new ExpandoObject();
            userInfo2.UserToken = q2.Data.UserToken;
            userInfo2.TimeLimit = 120;


            Response k = client.DoPostAsync("games", userInfo).Result;
            Response l = client.DoPostAsync("games", userInfo2).Result;
            int gameID = k.Data.GameID;


            Response t = client.DoGetAsync("games/" + gameID, "").Result;
            Assert.AreEqual("active", t.Data.GameState.ToString());
        }

        [TestMethod]
        public void TestPendingGame()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 120;

            dynamic userTokenInfo = new ExpandoObject();
            userTokenInfo.UserToken = users.UserToken;

            Response r = client.DoPostAsync("games", users).Result;

            Assert.AreEqual(Accepted, r.Status);

            client.DoPutAsync("games", userTokenInfo);

        }
    }
}
