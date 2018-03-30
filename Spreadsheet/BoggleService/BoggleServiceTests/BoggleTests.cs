using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static System.Net.HttpStatusCode;
using System.Diagnostics;
using System.Dynamic;
using System.Threading;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

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
        public static HashSet<string> dictionaryWords = new HashSet<string>();

        /// <summary>
        /// This is automatically run prior to all the tests to start the server
        /// </summary>
        [ClassInitialize()]
        public static void StartIIS(TestContext testContext)
        {
            IISAgent.Start(@"/site:""BoggleService"" /apppool:""Clr4IntegratedAppPool"" /config:""..\..\..\.vs\config\applicationhost.config""");
            string line;
            using (StreamReader file = new System.IO.StreamReader(AppDomain.CurrentDomain.BaseDirectory + "/dictionary.txt"))
            {
                while ((line = file.ReadLine()) != null)
                {
                    dictionaryWords.Add(line);
                }
            }
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
            userInfo.TimeLimit = 5;
            Response k = client.DoPostAsync("games", userInfo).Result;
            int gameID = k.Data.GameID;
            Response t = client.DoGetAsync("games/"+gameID, "").Result;
            Assert.AreEqual("pending", t.Data.GameState.ToString());

            // Clean up dangling pending player
            dynamic cleaner = new ExpandoObject();
            cleaner.Nickname = "Cleaner";
            q = client.DoPostAsync("users", cleaner).Result;
            dynamic cleanerInfo = new ExpandoObject();
            cleanerInfo.UserToken = q.Data.UserToken;
            cleanerInfo.TimeLimit = 120;
            k = client.DoPostAsync("games", cleanerInfo).Result;
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
        public void RegisterSeeIfServerReturnsUserToken()
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
            users.TimeLimit = 5;

            Response r = client.DoPostAsync("games", users).Result;
            users = new ExpandoObject();
            q = client.DoPutAsync(users, "games").Result;
            Assert.AreEqual(Accepted, r.Status);

            // Clean up dangling pending player
            dynamic cleaner = new ExpandoObject();
            cleaner.Nickname = "Cleaner";
            q = client.DoPostAsync("users", cleaner).Result;
            dynamic cleanerInfo = new ExpandoObject();
            cleanerInfo.UserToken = q.Data.UserToken;
            cleanerInfo.TimeLimit = 120;
            q = client.DoPostAsync("games", cleanerInfo).Result;
        }

        [TestMethod]
        public void JoinAsFirstPlayerAlreadyPendingExpectConflict()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 5;

            Response r = client.DoPostAsync("games", users).Result;
            r = client.DoPostAsync("games", users).Result;
            r = client.DoPostAsync("games", users).Result;
            Assert.AreEqual(Conflict, r.Status);

            // Clean up dangling pending player
            dynamic cleaner = new ExpandoObject();
            cleaner.Nickname = "Cleaner";
            q = client.DoPostAsync("users", cleaner).Result;
            dynamic cleanerInfo = new ExpandoObject();
            cleanerInfo.UserToken = q.Data.UserToken;
            cleanerInfo.TimeLimit = 120;
            q = client.DoPostAsync("games", cleanerInfo).Result;
        }

        [TestMethod]
        public void PlayIncorrectWordBasicTest()
        {
            // player 1, Jeb, register, join
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;
            users = new ExpandoObject();
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 80;
            q = client.DoPostAsync("games", users).Result;

            // p2, Bob, reg, join
            users = new ExpandoObject();
            users.Nickname = "Bob";
            q = client.DoPostAsync("users", users).Result;
            users = new ExpandoObject();
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 80;
            dynamic userPlayWord = new ExpandoObject();
            userPlayWord.UserToken = q.Data.UserToken;
            userPlayWord.Word = "testword";
            q = client.DoPostAsync("games", users).Result;

            Thread.Sleep(500);
            int gameID = q.Data.GameID;
            q = client.DoPutAsync(userPlayWord, "games/" + gameID.ToString()).Result;
            Assert.AreEqual(OK, q.Status);
            users = new ExpandoObject();
            users.Score = q.Data.Score;
            Assert.AreEqual("-1", users.Score.ToString());
        }

        [TestMethod]
        public void TestTwoLetterWord()
        {
            // player 1, Jeb, register, join
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;
            users = new ExpandoObject();
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 80;
            q = client.DoPostAsync("games", users).Result;

            // p2, Bob, reg, join
            users = new ExpandoObject();
            users.Nickname = "Bob";
            q = client.DoPostAsync("users", users).Result;
            users = new ExpandoObject();
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 80;
            dynamic userPlayWord = new ExpandoObject();
            userPlayWord.UserToken = q.Data.UserToken;
            userPlayWord.Word = "ok";
            q = client.DoPostAsync("games", users).Result;

            Thread.Sleep(500);
            int gameID = q.Data.GameID;
            q = client.DoPutAsync(userPlayWord, "games/" + gameID.ToString()).Result;
            Assert.AreEqual(OK, q.Status);
            users = new ExpandoObject();
            users.Score = q.Data.Score;
            Assert.AreEqual("0", users.Score.ToString());
        }

        [TestMethod]
        public void PlayWordOnTheBoard()
        {
            // player 1, Jeb, register, join
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;
            users = new ExpandoObject();
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 80;
            q = client.DoPostAsync("games", users).Result;

            // p2, Bob, reg, join
            users = new ExpandoObject();
            users.Nickname = "Bob";
            q = client.DoPostAsync("users", users).Result;
            users = new ExpandoObject();
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 80;
            dynamic userPlayWord = new ExpandoObject();
            userPlayWord.UserToken = q.Data.UserToken;
            userPlayWord.Word = "ok";
            q = client.DoPostAsync("games", users).Result;

            Thread.Sleep(500);
            int gameID = q.Data.GameID;
            Response r = client.DoGetAsync("games/" + gameID.ToString()).Result;
            string gameBoardLetters = r.Data.Board;

            BoggleBoard theBoard = new BoggleBoard(gameBoardLetters);
            bool didItPlay = false;
            foreach (string s in dictionaryWords)
            {
                if (theBoard.CanBeFormed(s) && s.Length > 2)
                {
                    userPlayWord.Word = s;
                    q = client.DoPutAsync(userPlayWord, "games/" + gameID.ToString()).Result;
                    Assert.AreEqual(OK, q.Status);
                    users = new ExpandoObject();
                    users.Score = q.Data.Score;
                    if (users.Score > 0)
                    {
                        didItPlay = true;
                        break;
                    }
                }
            }
            if (didItPlay == false)
                Assert.Fail();
        }

        [TestMethod]
        public void PlayWordOnTheBoardTwice()
        {
            // player 1, Jeb, register, join
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;
            users = new ExpandoObject();
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 80;
            q = client.DoPostAsync("games", users).Result;

            // p2, Bob, reg, join
            users = new ExpandoObject();
            users.Nickname = "Bob";
            q = client.DoPostAsync("users", users).Result;
            users = new ExpandoObject();
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 80;
            dynamic userPlayWord = new ExpandoObject();
            userPlayWord.UserToken = q.Data.UserToken;
            userPlayWord.Word = "ok";
            q = client.DoPostAsync("games", users).Result;

            Thread.Sleep(500);
            int gameID = q.Data.GameID;
            Response r = client.DoGetAsync("games/" + gameID.ToString()).Result;
            string gameBoardLetters = r.Data.Board;

            BoggleBoard theBoard = new BoggleBoard(gameBoardLetters);
            bool didItPlay = false;
            foreach (string s in dictionaryWords)
            {
                if (theBoard.CanBeFormed(s) && s.Length > 2)
                {
                    userPlayWord.Word = s;
                    q = client.DoPutAsync(userPlayWord, "games/" + gameID.ToString()).Result;
                    q = client.DoPutAsync(userPlayWord, "games/" + gameID.ToString()).Result;
                    Assert.AreEqual(OK, q.Status);
                    users = new ExpandoObject();
                    users.Score = q.Data.Score;
                    if (users.Score > -1)
                    {
                        Assert.AreEqual(users.Score.ToString(), "0");
                        didItPlay = true;
                        break;
                    }
                }
            }
            if (didItPlay == false)
                Assert.Fail();
        public void TestActiveBrief()
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


            Response t = client.DoGetAsync("games/" + gameID +"?brief=yes", "").Result;
            Assert.AreEqual("active", t.Data.GameState.ToString());
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

            Response t = client.DoGetAsync("games/" + gameID, "").Result;
            Response a;


                if (k.Status.Equals(Created))
                {
                    while (true)
                    {
                        a = client.DoGetAsync("games/" + gameID, "").Result;

                        if (a.Data.GameState.ToString().Equals("completed"))
                            break;
                    }
                }
                else if (k.Status.Equals(Accepted))
                {
                    while (true)
                    {
                        dynamic users3 = new ExpandoObject();
                        users3.Nickname = "Joe";
                        Response q3 = client.DoPostAsync("users", users).Result;

                        dynamic userInfo3 = new ExpandoObject();
                        userInfo3.UserToken = q3.Data.UserToken;
                        userInfo3.TimeLimit = 5;

                        Response m = client.DoPostAsync("games", userInfo3).Result;

                        a = client.DoGetAsync("games/" + gameID, "").Result;

                        if (a.Data.GameState.ToString().Equals("completed"))
                            break;
                    }
                }

            a = client.DoGetAsync("games/" + gameID, "").Result;
            Assert.AreEqual("completed", a.Data.GameState.ToString());



        }

        [TestMethod]
        public void TestActiveGameState()
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
            users.TimeLimit = 5;

            dynamic userTokenInfo = new ExpandoObject();
            userTokenInfo.UserToken = users.UserToken;

            Response r = client.DoPostAsync("games", users).Result;

            if (r.Status.Equals(Created))
            {
                dynamic users2 = new ExpandoObject();
                users2.Nickname = "Jeb";
                Response q2 = client.DoPostAsync("users", users2).Result;
                users2.UserToken = q2.Data.UserToken;
                users2.TimeLimit = 5;

                dynamic userTokenInfo2 = new ExpandoObject();
                userTokenInfo2.UserToken = users2.UserToken;

                Response r2 = client.DoPostAsync("games", users2).Result;

                Assert.AreEqual(Accepted, r2.Status);
            }
            else
            {
                Assert.AreEqual(Accepted, r.Status);
            }
        }

        [TestMethod]
        public void CancelJoin()
        {
            dynamic users = new ExpandoObject();
            users.Nickname = "Jeb";
            Response q = client.DoPostAsync("users", users).Result;
            users.UserToken = q.Data.UserToken;
            users.TimeLimit = 5;

            dynamic userTokenInfo = new ExpandoObject();
            userTokenInfo.UserToken = users.UserToken;

            Response k = client.DoPostAsync("games", users).Result;

            if (k.Status.Equals(Created))
            {
                dynamic users2 = new ExpandoObject();
                users2.Nickname = "Jeb";
                Response q2 = client.DoPostAsync("users", users2).Result;
                users2.UserToken = q.Data.UserToken;
                users2.TimeLimit = 5;

                dynamic userTokenInfo2 = new ExpandoObject();
                userTokenInfo2.UserToken = users2.UserToken;

                Response k2 = client.DoPostAsync("games", users2).Result;
                Response r2 = client.DoPutAsync(userTokenInfo2, "games").Result;
                Assert.AreEqual(OK, r2.Status);
            }
            else
            {
                Response r = client.DoPutAsync(userTokenInfo, "games").Result;
                Assert.AreEqual(OK, r.Status);
            }
        }
    }
}
