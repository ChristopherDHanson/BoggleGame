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
        private readonly Dictionary<string, Game> games = new Dictionary<string, Game>();
        private UserInfo pendingPlayer;
        private int gameCounter = 0;
        private string pendingGameID;
        private bool gameIsPending = false;
        private HashSet<string> dictionaryWords; // words that are valid inputs

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
            { // Creates a new pending game if none exists
                Game newGame = new Game();
                newGame.Player1Token = tkTime.UserToken;
                pendingGameID = gameCounter.ToString();
                newGame.GameID = pendingGameID;

                newGame.GameStatus = new GameStatus();
                newGame.GameStatus.GameState = "Pending";
                newGame.GameStatus.Player1 = new PlayerStatus();
                newGame.GameStatus.Player1.Nickname = users[tkTime.UserToken].Nickname;
                newGame.GameStatus.Player1.Score = 0;

                newGame.GameBoard = new BoggleBoard();
                newGame.GameStatus.Board = newGame.GameBoard.ToString();

                gameIsPending = true;
                gameCounter++;
                games.Add(pendingGameID, newGame);
            }

            if (!users.ContainsKey(tkTime.UserToken) || tkTime.TimeLimit < 5 || tkTime.TimeLimit > 120)
            {
                SetStatus(Forbidden);
                return null;
            }
            else if (pendingPlayer != null && pendingPlayer.UserToken.Equals(tkTime.UserToken)) // This user is already pending
            {
                SetStatus(Conflict);
                return null;
            }

            else if (pendingPlayer == null) { // No pending player exists; this user becomes pending player
                pendingPlayer = users[tkTime.UserToken];
                games[pendingGameID].GameStatus.TimeLimit = tkTime.TimeLimit;
                SetStatus(Accepted);
                return pendingGameID;
            }
            else // Second player found, match begins
            {
                games[pendingGameID].GameStatus.TimeLimit = (tkTime.TimeLimit + games[pendingGameID].GameStatus.TimeLimit) / 2;
                games[pendingGameID].GameStatus.GameState = "Active";

                games[pendingGameID].Player2Token = tkTime.UserToken;
                games[pendingGameID].GameStatus.Player2.Nickname = users[tkTime.UserToken].Nickname;
                games[pendingGameID].GameStatus.Player2.Score = 0;

                gameIsPending = false;
                SetStatus(Created);
                return pendingGameID;
            }
        }

        /// <summary>
        /// Cancels Join request if user is still in pending game
        /// </summary>
        /// <param name="userTkn">Contains UserToken</param>
        public void CancelJoin(Token userTkn)
        {
            if (!users.ContainsKey(userTkn.UserToken) || userTkn.UserToken.Equals(pendingPlayer.UserToken))
            {
                SetStatus(Forbidden);
            }
            else if(pendingPlayer != null && userTkn.UserToken.Equals(pendingPlayer.UserToken))
            { // Change user status in UserInfo, remove pending game
                users[userTkn.UserToken].GameStatus = "Registered";
                games.Remove(pendingGameID);
                pendingGameID = null;
                gameIsPending = false;
                pendingPlayer = null;
                SetStatus(OK);
            }
        }

        /// <summary>
        /// Plays a word in specified game of Boggle
        /// </summary>
        /// <param name="wordToPlay">Contains UserToken and Word</param>
        /// <param name="gameID">The GameID of target game</param>
        public void PlayWord(TokenWord wordToPlay, string gameID)
        {
            if (wordToPlay.Word == null || wordToPlay.Word.Equals("") || wordToPlay.Word.Trim().Length > 30
                || !games.ContainsKey(gameID) || !users.ContainsKey(wordToPlay.UserToken) ||
                !users[wordToPlay.UserToken].GameID.Equals(gameID))
            {
                SetStatus(Forbidden);
            }
            else if (!games[gameID].GameStatus.GameState.Equals("active"))
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

        /// <summary>
        /// Gets all
        /// </summary>
        /// <param name="isBrief"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public GameStatus GetStatus(string GameID, string isBrief)
        {
            if (games[GameID].GameStatus.Equals("pending"))
            {
                SetStatus(OK);
            }
            else if ((games[GameID].GameStatus.Equals("active") || games[GameID].GameStatus.Equals("completed")) &&
                     isBrief.Equals("yes"))
            {
                //                "GameState": "active",                   
                //                "TimeLeft": 32,                           
                //                "Player1": {
                //                    "Score": 3,
                //                },
                //                "Player2": {
                //                    "Score": 1,
                //                },
                SetStatus(OK);
            }
            else if (games[GameID].GameStatus.Equals("active") &&
                     !isBrief.Equals("yes"))
            {
//                "GameState": "active",
//                "Board": "ANETIXSRETAPLMON",
//                "TimeLimit": 120,
//                "TimeLeft": 32,
//                "Player1": {
//                    "Nickname": "Jack",
//                    "Score": 3,
//                },
//                "Player2": {
//                    "Nickname": "Jill",
//                    "Score": 1,
//                },
                SetStatus(OK);
            }
            else if (games[GameID].GameStatus.Equals("completed") &&
                     !isBrief.Equals("yes"))
            {
//                "GameState": "completed",
//                "Board": "ANETIXSRETAPLMON",
//                "TimeLimit": 120,
//                "TimeLeft": 0,
//                "Player1": {
//                    "Nickname": "Jack",
//                    "Score": 3,
//                    "WordsPlayed": [
//                    {"Word": "tine", "Score": 1},
//                    {"Word": "strap", "Score": 2} 
//                    ],
//                },
//                "Player2": {
//                    "Nickname": "Jill",
//                    "Score": 1,
//                    "WordsPlayed": [
//                    {"Word": "tin", "Score": 1}
//                    ],
//                },
                SetStatus(OK);
            }

            //to be modified
            return games[GameID].GameStatus;
        }
    }
}
