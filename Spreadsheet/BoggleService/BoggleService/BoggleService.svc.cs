﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ServiceModel.Web;
using static System.Net.HttpStatusCode;

namespace Boggle
{
    public class BoggleService : IBoggleService
    {
        private readonly Dictionary<string, UserName> users = new Dictionary<string, UserName>();
        private readonly Dictionary<string, GameStatus> games = new Dictionary<>(string, GameStatus);

        /// <summary>
        /// The most recent call to SetStatus determines the response code used when
        /// an http response is sent.
        /// </summary>
        /// <param name="status"></param>
        private static void SetStatus(HttpStatusCode status)
        {
            WebOperationContext.Current.OutgoingResponse.StatusCode = status;
        }

        /// <summary>
        /// Returns a Stream version of index.html.
        /// </summary>
        /// <returns></returns>
        public Stream API()
        {
            SetStatus(OK);
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/html";
            return File.OpenRead(AppDomain.CurrentDomain.BaseDirectory + "index.html");
        }

        /// <summary>
        /// Demo.  You can delete this.
        /// </summary>
        public string WordAtIndex(int n)
        {
            if (n < 0)
            {
                SetStatus(Forbidden);
                return null;
            }

            string line;
            using (StreamReader file = new System.IO.StreamReader(AppDomain.CurrentDomain.BaseDirectory + "dictionary.txt"))
            {
                while ((line = file.ReadLine()) != null)
                {
                    if (n == 0) break;
                    n--;
                }
            }

            if (n == 0)
            {
                SetStatus(OK);
                return line;
            }
            else
            {
                SetStatus(Forbidden);
                return null;
            }
        }

        public string Register(UserName name)
        {
            string newUserToken = "";
            users.Add(newUserToken, name);
            return newUserToken;
        }

        public string Join(TokenTime tkTime)
        {
            string newGameID = "";
            tkTime.UserToken;
            tkTime.Time;
            games.Add(newGameID, ??);
            return newGameID;
        }

        public void CancelJoin(Token userTkn)
        {

        }

        public void PlayWord(TokenWord wordToPlay, string gameID)
        {

        }

        public GameStatus GetAllItems(string isBrief, string userID)
        {
            return null;
        }
    }
}
