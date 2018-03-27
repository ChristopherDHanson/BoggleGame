using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ServiceModel.Web;
using static System.Net.HttpStatusCode;

namespace Boggle
{
    public class BoggleService : IBoggleService
    {
        private readonly Dictionary<string, UserInfo> users = new Dictionary<string, UserInfo>();
        private readonly Dictionary<string, GameStatus> games = new Dictionary<string, GameStatus>();
        private UserInfo pendingPlayer;


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
            games.Add(newGameID, );
            return newGameID;
        }

        public void CancelJoin(Token userTkn)
        {
            if (!users.ContainsKey(userTkn.UserToken) || userTkn.UserToken.Equals(pendingPlayer.UserToken))
            {
                SetStatus(Forbidden);
            }
            else if(pendingPlayer != null && userTkn.UserToken.Equals(pendingPlayer.UserToken))
            {
                users[userTkn.UserToken].GameStatus = "Registered";
                pendingPlayer = null;
                SetStatus(OK);
            }
        }

        public void PlayWord(TokenWord wordToPlay, string gameID)
        {
            if (wordToPlay.Word == null || wordToPlay.Word.Equals("") || wordToPlay.Word.Trim().Length > 30
                || !games.ContainsKey(gameID) || !users.ContainsKey(wordToPlay.UserToken) ||
                users[wordToPlay.UserToken].GameID.Equals(gameID))
            {
                SetStatus(Forbidden);
            }
            else if (!games[gameID].GameState.Equals("active"))
            {
                SetStatus(Conflict);
            }
            else
            {
//                Otherwise, records the trimmed Word as being played by UserToken in the game identified by GameID.

//                Returns the score for Word in the context of the game(e.g. if Word has been played before the score is zero). 
//                The word is not case sensitive.

//                Responds with status 200(OK).
                SetStatus(OK);
            }
        }

        public GameStatus GetAllItems(string isBrief, string userID)
        {
            return null;
        }
    }
}
