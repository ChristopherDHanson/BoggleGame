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
        private readonly Dictionary<string, UserName> users = new Dictionary<string, UserName>();
        private readonly Dictionary<string, Game> games = new Dictionary<string, Game>();
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
                users.Add(newUserToken, name);
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
            if (!users.ContainsKey(tkTime.UserToken) || tkTime.TimeLimit < 5 || tkTime.TimeLimit > 120)
            {
                SetStatus(Forbidden);
                return null;
            }

            if (!gameIsPending)
            { // Creates a new pending game if none exists, adds this player as P1
                Game newGame = new Game();
                newGame.Player1Token = tkTime.UserToken;
                pendingGameID = gameCounter.ToString();
                newGame.GameID = pendingGameID;

                newGame.GameStatus = new GameStatus();
                newGame.GameStatus.GameState = "pending";
                newGame.GameStatus.Player1 = new PlayerStatus();
                newGame.GameStatus.Player1.Nickname = users[tkTime.UserToken].Nickname;
                newGame.GameStatus.Player1.Score = 0;

                newGame.GameBoard = new BoggleBoard();
                newGame.GameStatus.Board = newGame.GameBoard.ToString();

                gameIsPending = true;
                gameCounter++;
                games.Add(pendingGameID, newGame);
                games[pendingGameID].GameStatus.TimeLimit = tkTime.TimeLimit;

                return pendingGameID;
            }
            else if (gameIsPending && games[pendingGameID].Player1Token.Equals(tkTime.UserToken)) // This user is already pending
            {
                SetStatus(Conflict);
                return null;
            }
            else // Second player found, match begins
            {
                games[pendingGameID].GameStatus.TimeLimit = (tkTime.TimeLimit + games[pendingGameID].GameStatus.TimeLimit) / 2;
                games[pendingGameID].GameStatus.GameState = "active";

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
            if (!users.ContainsKey(userTkn.UserToken) || userTkn.UserToken.Equals(games[pendingGameID].Player1Token))
            {
                SetStatus(Forbidden);
            }
            else if(gameIsPending && userTkn.UserToken.Equals(games[pendingGameID].Player1Token))
            { // Remove pending game
                games.Remove(pendingGameID);
                pendingGameID = null;
                gameIsPending = false;
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
                (!games[gameID].Player1Token.Equals(wordToPlay.UserToken) && !games[gameID].Player2Token.Equals(wordToPlay.UserToken)))
            {
                SetStatus(Forbidden);
            }
            else if (!games[gameID].GameStatus.GameState.Equals("active"))
            {
                SetStatus(Conflict);
            }
            else // Word will be successfully played
            {
                //                Otherwise, records the trimmed Word as being played by UserToken in the game identified by GameID.
                //                Returns the score for Word in the context of the game(e.g. if Word has been played before the score is zero). 
                //                The word is not case sensitive.
                string theWord = wordToPlay.Word.Trim().ToLower();
                string theToken = wordToPlay.UserToken;

                if (games[gameID].GameBoard.CanBeFormed(theWord) && dictionaryWords.Contains(theWord) && !HasBeenPlayed(wordToPlay.UserToken, gameID, wordToPlay.Word)) //+its in dictionary and if it has not been played before
                {
                    //add to words played and increment point 
                    if (games[gameID].Player1Token.Equals(theToken)) // user is Player1
                    {
                        games[gameID].GameStatus.Player1.Score++;
                        games[gameID].GameStatus.Player1.WordsPlayed.Add(new WordScore(theWord, 1));
                    }
                    else // user is Player2
                    {
                        games[gameID].GameStatus.Player2.Score++;
                        games[gameID].GameStatus.Player2.WordsPlayed.Add(new WordScore(theWord, 1));
                    }
                }
                else if (games[gameID].GameBoard.CanBeFormed(theWord) && HasBeenPlayed(wordToPlay.UserToken, gameID, wordToPlay.Word))//if+its in dictionary and if it has been played before
                {
                    //add to words played with 0 points
                    if (games[gameID].Player1Token.Equals(theToken)) // user is Player1
                    {
                        games[gameID].GameStatus.Player1.WordsPlayed.Add(new WordScore(theWord, 0));
                    }
                    else // user is Player2
                    {
                        games[gameID].GameStatus.Player2.WordsPlayed.Add(new WordScore(theWord, 0));
                    }
                }
                else // Invalid word played
                {
                    //add to words played and decrement a point
                    if (games[gameID].Player1Token.Equals(theToken)) // user is Player1
                    {
                        games[gameID].GameStatus.Player1.Score--;
                        games[gameID].GameStatus.Player1.WordsPlayed.Add(new WordScore(theWord, -1));
                    }
                    else // user is Player2
                    {
                        games[gameID].GameStatus.Player2.Score--;
                        games[gameID].GameStatus.Player2.WordsPlayed.Add(new WordScore(theWord, -1));
                    }
                }

                // Responds with status 200(OK).
                SetStatus(OK);
            }
        }

        /// <summary>
        /// Gets and returns the GameStatus of specified game
        /// </summary>
        /// <param name="GameID">The GameID of target Game</param>
        /// <param name="isBrief">"yes" = brief, anything else = not brief</param>
        public GameStatus GetStatus(string GameID, string isBrief)
        {
            if (!games.ContainsKey(GameID))
            {
                SetStatus(Forbidden);
                return null;
            }
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
            else if (games[GameID].GameStatus.Equals("active") && !isBrief.Equals("yes"))
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
            else if (games[GameID].GameStatus.Equals("completed") && !isBrief.Equals("yes"))
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

        private bool HasBeenPlayed(string userToken, string gameID, string targetWord)
        {
            IList<WordScore> tempList;

            if (games[gameID].Player1Token.Equals(userToken))
            {
                tempList = games[gameID].GameStatus.Player1.WordsPlayed;

                foreach (WordScore word in tempList)
                {
                    if (word.Word.Equals(targetWord))
                        return true;
                }
            }
            else
            {
                tempList = games[gameID].GameStatus.Player2.WordsPlayed;

                foreach (WordScore word in tempList)
                {
                    if (word.Word.Equals(targetWord))
                        return true;
                }
            }

            return false;
        }
    }
}
