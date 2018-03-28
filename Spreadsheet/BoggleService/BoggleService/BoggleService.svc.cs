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
        private readonly Dictionary<string, UserInfo> users = new Dictionary<string, UserInfo>();
        private readonly Dictionary<string, Game> games = new Dictionary<string, Game>();
        private UserInfo pendingPlayer;
        private int gameCounter = 0;
        private string pendingGameID = 0;
        private bool gameIsPending = false;
        private HashSet<string> dictionaryWords = null; // words that are valid inputs

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

        /// <summary>
        /// Register new user
        /// </summary>
        /// <param name="name">Contains Nickname</param>
        /// <returns>Returns user token</returns>
        public string Register(UserName name)
        {
            string theName = name.Nickname;
            if (theName == null)
            {
                SetStatus(Forbidden);
                return null;
            }
            theName = theName.Trim();
            if (theName.Length == 0 || theName.Length > 50)
            {
                SetStatus(Forbidden);
                return null;
            }
            else {
                string newUserToken = Guid.NewGuid().ToString();
                UserInfo newUser = new UserInfo();
                newUser.Nickname = theName;
                newUser.GameStatus = "Registered";
                newUser.UserToken = newUserToken;
                users.Add(newUserToken, newUser);
                SetStatus(Created);
                return newUserToken;
            }
        }

        /// <summary>
        /// Join new game
        /// </summary>
        /// <param name="tkTime">Contains UserToken and desired TimeLimit</param>
        /// <returns>Returns new GameID</returns>
        public string Join(TokenTime tkTime)
        {
            if (!gameIsPending)
            {
                string newGameID = gameCounter.ToString();
                pendingGameID = newGameID;
                gameCounter++;
                Game newGame = new Game();
                GameStatus newGameStatus = new GameStatus();
                games.Add(newGameID, newGame);
                gameIsPending = true;
            }

            if (!users.ContainsKey(tkTime.UserToken) || tkTime.TimeLimit < 5 || tkTime.TimeLimit > 120)
            {
                SetStatus(Forbidden);
                return null;
            }
            else if (pendingPlayer != null && pendingPlayer.UserToken.Equals(tkTime.UserToken))
            {
                SetStatus(Conflict);
                return null;
            }
            else if (pendingPlayer == null) {
                users[tkTime.UserToken] = pendingPlayer;
                newGame.TimeLimit = tkTime.TimeLimit;
                return newGameID;
            }
            else // Two players, match begins
            {
                newGame.TimeLimit = (tkTime.TimeLimit + newGame.TimeLimit) / 2;
                games.Add(newGameID, newGame);
                gameIsPending = false;
                return newGameID;
            }
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
            else if (!games[gameID].GameStatus.Equals("active"))
            {
                SetStatus(Conflict);
            }
            else
            {
                //                Otherwise, records the trimmed Word as being played by UserToken in the game identified by GameID.
                //                Returns the score for Word in the context of the game(e.g. if Word has been played before the score is zero). 
                //                The word is not case sensitive.

                if (games[users[wordToPlay.UserToken].GameID].GameBoard.CanBeFormed(wordToPlay.Word.Trim())) //+its in dictionary and if it has not been played before
                {
                    //add to words played and increment point 
                }
                else if (true)//if+its in dictionary and if it has been played before
                {
                    //add to words played with 0 points
                }
                else
                {
                    //add to words played and decrement a point
                }

                //update player status
                //update game status
                //update Game



                //                Responds with status 200(OK).
                SetStatus(OK);
            }
        }

        public GameStatus GetAllItems(string isBrief, string userID)
        {
                //to be modified
                return games[users[userID].GameID].GameStatus;
        }
    }
}
