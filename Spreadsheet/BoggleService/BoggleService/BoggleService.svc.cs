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
        private readonly static Dictionary<string, UserName> users = new Dictionary<string, UserName>();
        private readonly static Dictionary<string, Game> games = new Dictionary<string, Game>();
        private static int gameCounter = 0;
        private static string pendingGameID;
        private static bool gameIsPending = false;
        private static HashSet<string> dictionaryWords = new HashSet<string>(); // words that are valid inputs
        private static readonly object sync = new object();

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
        public Token Register(UserName name)
        {
            lock (sync)
            {
                if (dictionaryWords == null)
                { // The first time a user registers to the server, copy contents of .txt file into HashSet for const. access
                    string line;
                    using (StreamReader file = new System.IO.StreamReader(AppDomain.CurrentDomain.BaseDirectory + "dictionary.txt"))
                    {
                        while ((line = file.ReadLine()) != null)
                        {
                            dictionaryWords.Add(line);
                        }
                    }
                }

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
                else
                {
                    string newUserToken = Guid.NewGuid().ToString();
                    users.Add(newUserToken, name);
                    SetStatus(Created);
                    Token returnToke = new Token();
                    returnToke.UserToken = newUserToken;
                    return returnToke;
                }
            }
        }

        /// <summary>
        /// Join new game
        /// </summary>
        /// <param name="tkTime">Contains UserToken and desired TimeLimit</param>
        /// <returns>Returns new GameID</returns>
        public GameIDOnly Join(TokenTime tkTime)
        {
            lock (sync)
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
                    newGame.GameStatus.Player1.WordsPlayed = new List<WordScore>();
                    newGame.GameStatus.Player1.Nickname = users[tkTime.UserToken].Nickname;
                    newGame.GameStatus.Player1.Score = 0;

                    newGame.GameBoard = new BoggleBoard();
                    newGame.GameStatus.Board = newGame.GameBoard.ToString();

                    gameIsPending = true;
                    gameCounter++;
                    games.Add(pendingGameID, newGame);
                    games[pendingGameID].GameStatus.TimeLimit = tkTime.TimeLimit;

                    SetStatus(Accepted);
                    GameIDOnly idToReturn = new GameIDOnly();
                    idToReturn.GameID = pendingGameID;
                    return idToReturn;
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
                    games[pendingGameID].GameStatus.Player2 = new PlayerStatus();
                    games[pendingGameID].GameStatus.Player2.WordsPlayed = new List<WordScore>();
                    games[pendingGameID].GameStatus.Player2.Nickname = users[tkTime.UserToken].Nickname;
                    games[pendingGameID].GameStatus.Player2.Score = 0;

                    gameIsPending = false;
                    SetStatus(Created);
                    GameIDOnly idToReturn = new GameIDOnly();
                    idToReturn.GameID = pendingGameID;
                    games[pendingGameID].StartTime = Environment.TickCount;
                    return idToReturn;
                }
            }
        }

        /// <summary>
        /// Cancels Join request if user is still in pending game
        /// </summary>
        /// <param name="userTkn">Contains UserToken</param>
        public void CancelJoin(Token userTkn)
        {
            lock (sync)
            {
                if (!users.ContainsKey(userTkn.UserToken) || !userTkn.UserToken.Equals(games[pendingGameID].Player1Token))
                {
                    SetStatus(Forbidden);
                }
                else if (gameIsPending && userTkn.UserToken.Equals(games[pendingGameID].Player1Token))
                { // Remove pending game
                    games.Remove(pendingGameID);
                    pendingGameID = null;
                    gameIsPending = false;
                    SetStatus(OK);
                }
            }
        }

        /// <summary>
        /// Plays a word in specified game of Boggle
        /// </summary>
        /// <param name="wordToPlay">Contains UserToken and Word</param>
        /// <param name="gameID">The GameID of target game</param>
        public ScoreOnly PlayWord(TokenWord wordToPlay, string gameID)
        {
            lock (sync)
            {
                if (wordToPlay.Word == null || wordToPlay.Word.Equals("") || wordToPlay.Word.Trim().Length > 30
                    || !games.ContainsKey(gameID) || !users.ContainsKey(wordToPlay.UserToken) ||
                    (!games[gameID].Player1Token.Equals(wordToPlay.UserToken) && !games[gameID].Player2Token.Equals(wordToPlay.UserToken)))
                {
                    SetStatus(Forbidden);
                    return null;
                }
                else if (!games[gameID].GameStatus.GameState.Equals("active"))
                {
                    SetStatus(Conflict);
                    return null;
                }
                else // Word will be successfully played
                {
                    string theWord = wordToPlay.Word.Trim().ToLower();
                    string theToken = wordToPlay.UserToken;
                    ScoreOnly scoreToReturn = new ScoreOnly();
                    int tempScore;

                    if (games[gameID].GameBoard.CanBeFormed(theWord) && dictionaryWords.Contains(theWord) &&
                        !HasBeenPlayed(wordToPlay.UserToken, gameID, wordToPlay.Word))
                    {
                        if (theWord.Length < 3)
                            tempScore = 0;
                        else if (theWord.Length == 3 || theWord.Length == 4)
                            tempScore = 1;
                        else if (theWord.Length == 5)
                            tempScore = 2;
                        else if (theWord.Length == 6)
                            tempScore = 3;
                        else if (theWord.Length == 7)
                            tempScore = 5;
                        else
                            tempScore = 11;

                        //add to words played and increment point 
                        if (games[gameID].Player1Token.Equals(theToken)) // user is Player1
                        {
                            games[gameID].GameStatus.Player1.Score += tempScore;
                            games[gameID].GameStatus.Player1.WordsPlayed.Add(new WordScore(theWord, tempScore));
                        }
                        else // user is Player2
                        {
                            games[gameID].GameStatus.Player2.Score += tempScore;
                            games[gameID].GameStatus.Player2.WordsPlayed.Add(new WordScore(theWord, tempScore));
                        }
                        scoreToReturn.Score = tempScore;
                    }
                    else if (games[gameID].GameBoard.CanBeFormed(theWord) && dictionaryWords.Contains(theWord) &&
                        HasBeenPlayed(wordToPlay.UserToken, gameID, wordToPlay.Word))
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
                        scoreToReturn.Score = 0;
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
                        scoreToReturn.Score = -1;
                    }

                    // Responds with status 200(OK).
                    SetStatus(OK);
                    return scoreToReturn;
                }
            }
        }

        /// <summary>
        /// Gets and returns the GameStatus of specified game
        /// </summary>
        /// <param name="GameID">The GameID of target Game</param>
        /// <param name="isBrief">"yes" = brief, anything else = not brief</param>
        public GameStatus GetStatus(string GameID, string isBrief)
        {
            lock (sync)
            {
                if (!games.ContainsKey(GameID))
                {
                    SetStatus(Forbidden);
                    return null;
                }
                if (games[GameID].GameStatus.Equals("pending"))
                {
                    GameStatus pending = new GamePending();
                    pending.GameState = games[GameID].GameStatus.GameState;
                    SetStatus(OK);

                    return pending;
                }

                int timeRemaining = games[GameID].GameStatus.TimeLimit - ((Environment.TickCount - games[GameID].StartTime)/1000);
                if (timeRemaining <= 0) {
                    games[GameID].GameStatus.TimeLeft = 0;
                    games[GameID].GameStatus.GameState = "completed";
                }
                else
                {
                    games[GameID].GameStatus.TimeLeft = timeRemaining;
                }


                if ((games[GameID].GameStatus.GameState.Equals("active") || games[GameID].GameStatus.GameState.Equals("completed")) &&
                         isBrief.Equals("yes"))
                {
                    GameStatus activeCompleteBrief = new GameFull();
                    activeCompleteBrief.GameState = games[GameID].GameStatus.GameState;
                    activeCompleteBrief.TimeLeft = games[GameID].GameStatus.TimeLeft;
                    activeCompleteBrief.Player1 = games[GameID].GameStatus.Player1;
                    activeCompleteBrief.Player2 = games[GameID].GameStatus.Player2;
                    SetStatus(OK);
                    return activeCompleteBrief;
                }

                if (games[GameID].GameStatus.GameState.Equals("active") && !isBrief.Equals("yes"))
                {
                    GameStatus activeBrief = new GameActiveBrief();
                    activeBrief.GameState = games[GameID].GameStatus.GameState;
                    activeBrief.TimeLeft = games[GameID].GameStatus.TimeLeft;
                    activeBrief.TimeLimit = games[GameID].GameStatus.TimeLimit;
                    activeBrief.Player1 = games[GameID].GameStatus.Player1;
                    activeBrief.Player2 = games[GameID].GameStatus.Player2;
                    SetStatus(OK);
                    return activeBrief;
                }

                if (games[GameID].GameStatus.GameState.Equals("completed") && !isBrief.Equals("yes"))
                {
                    SetStatus(OK);
                }

                return games[GameID].GameStatus;
            }
        }

        private bool HasBeenPlayed(string userToken, string gameID, string targetWord)
        {
            lock (sync)
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
}
