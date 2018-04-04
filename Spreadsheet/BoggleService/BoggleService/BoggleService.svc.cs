﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.ServiceModel.Web;
using static System.Net.HttpStatusCode;

/// <summary>
/// This namespace acts as the logic for a server that users can connect to, in order to play other users in a Bogglegame. It implements
/// IBoggleService, and uses BoggleBoard for the board layout.
/// 
/// Authors: Bryce Hansen, Chris Hanson
/// </summary>
namespace Boggle
{
    public class BoggleService : IBoggleService
    {
        //Static variables that act as a database for the users and games on the server
        private readonly static Dictionary<string, UserName> users = new Dictionary<string, UserName>();
        private readonly static Dictionary<string, Game> games = new Dictionary<string, Game>();

        //helper variables for users looking for a game, and the number of games on server
        private static int gameCounter = 0;
        private static string pendingGameID;
        private static bool gameIsPending = false;

        //holds the dictionary of valid and playable words.
        private static HashSet<string> dictionaryWords = new HashSet<string>(); // words that are valid inputs
        private static readonly object sync = new object();
        private static string BoggleServiceDB;

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
        /// Register new user
        /// </summary>
        /// <param name="name">Contains Nickname</param>
        /// <returns>Returns user token</returns>
        public Token Register(UserName name)
        {
            lock (sync)
            {
                if (dictionaryWords.Count == 0)
                {
                    // The first time a user registers to the server, copy contents of .txt file into HashSet for const. access
                    string line;
                    using (StreamReader file =
                        new System.IO.StreamReader(AppDomain.CurrentDomain.BaseDirectory + "dictionary.txt"))
                    {
                        while ((line = file.ReadLine()) != null)
                        {
                            dictionaryWords.Add(line);
                        }
                    }
                }

                //Returns null if name is invalid
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
                //must be a valid UserToken
                if (!users.ContainsKey(tkTime.UserToken) || tkTime.TimeLimit < 5 || tkTime.TimeLimit > 120)
                {
                    SetStatus(Forbidden);
                    return null;
                }

                if (!gameIsPending)
                {
                    // Creates a new pending game if none exists, adds this player as P1
                    Game newGame = new Game();
                    newGame.Player1Token = tkTime.UserToken;
                    pendingGameID = gameCounter.ToString();
                    newGame.GameID = pendingGameID;

                    //initialize player information in DataModel
                    newGame.GameStatus = new GameStatus();
                    newGame.GameStatus.GameState = "pending";
                    newGame.GameStatus.Player1 = new PlayerStatus();
                    newGame.GameStatus.Player1.WordsPlayed = new List<WordScore>();
                    newGame.GameStatus.Player1.Nickname = users[tkTime.UserToken].Nickname;
                    newGame.GameStatus.Player1.Score = 0;
                    newGame.GameBoard = new BoggleBoard();
                    newGame.GameStatus.Board = newGame.GameBoard.ToString();

                    //Add player to game
                    gameIsPending = true;
                    gameCounter++;
                    games.Add(pendingGameID, newGame);
                    games[pendingGameID].GameStatus.TimeLimit = tkTime.TimeLimit;

                    //update server status
                    SetStatus(Accepted);
                    GameIDOnly idToReturn = new GameIDOnly();
                    idToReturn.GameID = pendingGameID;
                    return idToReturn;
                }
                else if (gameIsPending && games[pendingGameID].Player1Token.Equals(tkTime.UserToken)
                ) // This user is already pending
                {
                    SetStatus(Conflict);
                    return null;
                }
                else // Second player found, match begins
                {
                    games[pendingGameID].GameStatus.TimeLimit =
                        (tkTime.TimeLimit + games[pendingGameID].GameStatus.TimeLimit) / 2;
                    games[pendingGameID].GameStatus.GameState = "active";

                    //initialize player3's information and DataModel
                    games[pendingGameID].Player2Token = tkTime.UserToken;
                    games[pendingGameID].GameStatus.Player2 = new PlayerStatus();
                    games[pendingGameID].GameStatus.Player2.WordsPlayed = new List<WordScore>();
                    games[pendingGameID].GameStatus.Player2.Nickname = users[tkTime.UserToken].Nickname;
                    games[pendingGameID].GameStatus.Player2.Score = 0;

                    //update game and server status
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
                //must be a user that is pending finding a game.
                if (!users.ContainsKey(userTkn.UserToken) ||
                    !userTkn.UserToken.Equals(games[pendingGameID].Player1Token))
                {
                    SetStatus(Forbidden);
                }
                else if (gameIsPending && userTkn.UserToken.Equals(games[pendingGameID].Player1Token))
                {
                    // Remove pending game
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
                //if not a valid wordm return null and update server.
                if (wordToPlay.Word == null || wordToPlay.Word.Equals("") || wordToPlay.Word.Trim().Length > 30 ||
                    !games.ContainsKey(gameID) || !users.ContainsKey(wordToPlay.UserToken) ||
                    (!games[gameID].Player1Token.Equals(wordToPlay.UserToken) &&
                     !games[gameID].Player2Token.Equals(wordToPlay.UserToken)))
                {
                    SetStatus(Forbidden);
                    return null;
                } // if game isn't active, update server with a conflict status
                else if (!games[gameID].GameStatus.GameState.Equals("active"))
                {
                    SetStatus(Conflict);
                    return null;
                }
                else // Word will be successfully played
                {
                    //trim and save word with token
                    string theWord = wordToPlay.Word.Trim().ToUpper();
                    string theToken = wordToPlay.UserToken;
                    ScoreOnly scoreToReturn = new ScoreOnly();
                    int tempScore;
                    tempScore = 0;

                    //update scores according to word length
                    if (games[gameID].GameBoard.CanBeFormed(theWord) && dictionaryWords.Contains(theWord) &&
                        !HasBeenPlayed(wordToPlay.UserToken, gameID, wordToPlay.Word))
                    {
                        if (theWord.Length == 3 || theWord.Length == 4) tempScore = 1;
                        else if (theWord.Length == 5)
                            tempScore = 2;
                        else if (theWord.Length == 6)
                            tempScore = 3;
                        else if (theWord.Length == 7)
                            tempScore = 5;
                        else if (theWord.Length > 7)
                            tempScore = 11;

                        //add to words played and increment point
                        WordScore wordScoreToAdd = new WordScore();
                        wordScoreToAdd.Word = theWord;
                        wordScoreToAdd.Score = tempScore;
                        if (games[gameID].Player1Token.Equals(theToken)) // user is Player1
                        {
                            games[gameID].GameStatus.Player1.Score += tempScore;
                            games[gameID].GameStatus.Player1.WordsPlayed.Add(wordScoreToAdd);
                        }
                        else // user is Player2
                        {
                            games[gameID].GameStatus.Player2.Score += tempScore;
                            games[gameID].GameStatus.Player2.WordsPlayed.Add(wordScoreToAdd);
                        }
                    }
                    else if (games[gameID].GameBoard.CanBeFormed(theWord) && dictionaryWords.Contains(theWord) &&
                             HasBeenPlayed(wordToPlay.UserToken, gameID, wordToPlay.Word))
                    {
                        //add to words played with 0 points
                        WordScore wordScoreToAdd = new WordScore();
                        wordScoreToAdd.Word = theWord;
                        wordScoreToAdd.Score = tempScore;
                        if (games[gameID].Player1Token.Equals(theToken)) // user is Player1
                        {
                            games[gameID].GameStatus.Player1.WordsPlayed.Add(wordScoreToAdd);
                        }
                        else // user is Player2
                        {
                            games[gameID].GameStatus.Player2.WordsPlayed.Add(wordScoreToAdd);
                        }
                    }
                    else // Invalid word played
                    {
                        if (theWord.Length > 2)
                        {
                            tempScore = -1;
                        }

                        //add to words played and decrement a point
                        WordScore wordScoreToAdd = new WordScore();
                        wordScoreToAdd.Word = theWord;
                        wordScoreToAdd.Score = tempScore;
                        if (games[gameID].Player1Token.Equals(theToken)) // user is Player1
                        {
                            games[gameID].GameStatus.Player1.Score += tempScore;
                            games[gameID].GameStatus.Player1.WordsPlayed.Add(wordScoreToAdd);
                        }
                        else // user is Player2
                        {
                            games[gameID].GameStatus.Player2.Score += tempScore;
                            games[gameID].GameStatus.Player2.WordsPlayed.Add(wordScoreToAdd);
                        }
                    }

                    SetStatus(OK);
                    scoreToReturn.Score = tempScore;
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
            using (SqlConnection conn = new SqlConnection(BoggleServiceDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand cmd = new SqlCommand("select GameID from Games where GameID = @GameID", conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@GameID", GameID);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                SetStatus(Forbidden);
                                trans.Commit();
                                return null;
                            }
                        }
                    }

                    int startTime;
                    String query = "select Player1, Player2, Board, TimeLimit, StartTime from Games, GameID where Games.GameID = @GameID";
                    using (SqlCommand command = new SqlCommand(query, conn, trans))
                    {
                        command.Parameters.AddWithValue("@GameID", GameID);
                        GameStatus toReturn = new GameStatus();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            toReturn.Board = (string) reader["Board"];
                            toReturn.TimeLeft = (int) reader["TimeLeft"];
                            toReturn.TimeLimit = (int) reader["TimeLimit"];

                            query =
                                "select Word, GameID, Player, Score from Words, GameID where Words.GameID = @GameID and Player where Words.Player = @Player";
                            using (SqlCommand playerStatus = new SqlCommand(query, conn, trans))
                            {
                                //this block extracts nicknames from games user tokens.
                                String p1name = (string) reader["Player1"];
                                String p2name = (string) reader["Player2"];
                                query = "select Nickname from Users, Nickname where Users.UserToken = @UserToken";
                                using (SqlCommand player1Nickname = new SqlCommand(query, conn, trans))
                                {
                                    player1Nickname.Parameters.AddWithValue("@UserToken", p1name);
                                    using (SqlDataReader nameReader = player1Nickname.ExecuteReader())
                                    {
                                        p1name = (string) nameReader["Nickname"];
                                    }
                                }

                                using (SqlCommand player2Nickname = new SqlCommand(query, conn, trans))
                                {
                                    player2Nickname.Parameters.AddWithValue("@UserToken", p2name);
                                    using (SqlDataReader nameReader = player2Nickname.ExecuteReader())
                                    {
                                        p2name = (string) nameReader["Nickname"];
                                    }
                                }

                                int p1Score = 0;
                                int p2Score = 0;
                                IList<WordScore> p1WordList = new List<WordScore>();
                                IList<WordScore> p2WordList = new List<WordScore>();
                                playerStatus.Parameters.AddWithValue("@GameID", GameID);
                                playerStatus.Parameters.AddWithValue("@Player", (string) reader["Player1"]);
                                using (SqlDataReader wordAndScoreP1 = playerStatus.ExecuteReader())
                                {
                                    while (wordAndScoreP1.Read())
                                    {
                                        WordScore tempWS = new WordScore();
                                        tempWS.Word = (string) wordAndScoreP1["Word"];
                                        tempWS.Score = (int) wordAndScoreP1["Score"];
                                        p1WordList.Add(tempWS);
                                        p1Score += (int) wordAndScoreP1["Score"];
                                    }
                                }

                                playerStatus.Parameters.AddWithValue("@GameID", GameID);
                                playerStatus.Parameters.AddWithValue("@Player", (string) reader["Player2"]);
                                using (SqlDataReader wordAndScoreP2 = playerStatus.ExecuteReader())
                                {
                                    while (wordAndScoreP2.Read())
                                    {
                                        WordScore tempWS = new WordScore();
                                        tempWS.Word = (string) wordAndScoreP2["Word"];
                                        tempWS.Score = (int) wordAndScoreP2["Score"];
                                        p2WordList.Add(tempWS);
                                        p2Score += (int) wordAndScoreP2["Score"];
                                    }
                                }

                                toReturn.Player1 = (new PlayerStatus()
                                {
                                    Nickname = p1name,
                                    Score = p1Score,
                                    WordsPlayed = p1WordList
                                }); //create new PlayerStatus obj from this token to info in Users table DB

                                toReturn.Player2 = (new PlayerStatus
                                {
                                    Nickname = p2name,
                                    Score = p2Score,
                                    WordsPlayed = p2WordList
                                }); //create new PlayerStatus obj from this token to info in Users table DB

                            }

                            DateTime temp = (DateTime)reader["StartTime"];
                            startTime = temp.Millisecond;
                        }

                        //Return logic goes here
                                //returns needed status update if pending, active, or completed and if isBrief == yes
                                if (toReturn.Player2 == null)
                                {
                                    toReturn.GameState = "pending";
                                    toReturn.Board = null;
                                    toReturn.Player1 = null;
                                    toReturn.Player2 = null;
                                    toReturn.TimeLeft = null;
                                    toReturn.TimeLimit = null;
                                }
                                else
                                {
                                    //calculate time remaining to return
                                    int timeNow = Environment.TickCount;
                                    int? timeRemaining = toReturn.TimeLimit -
                                                            ((timeNow - startTime) / 1000);
                        
                                    //if timeremaining is 0 return completed
                                    if (timeRemaining <= 0)
                                    {
                                        toReturn.TimeLeft = 0;
                                        toReturn.GameState = "completed";
                                    }
                                    else
                                    {
                                        toReturn.TimeLeft = timeRemaining;
                                    }
                        
                                    //active or completed and brief response, return necessary format and update datamodel
                                    if (toReturn.Player2 != null && (isBrief != null && isBrief.Equals("yes"))) // Active or completed, brief response
                                    {
                                        if (!(timeRemaining <= 0))
                                            toReturn.GameState = "active";

                                        toReturn.Board = null;
                                        toReturn.Player1 = new PlayerStatus();
                                        toReturn.Player1.Score = toReturn.Player1.Score;
                                        toReturn.Player2 = new PlayerStatus();
                                        toReturn.Player2.Score = toReturn.Player2.Score;
                                        toReturn.TimeLeft = toReturn.TimeLeft;
                                        toReturn.TimeLimit = null;
                                    }
                                    //else return non brief responses
                                    else if (toReturn.GameState.Equals("completed") &&
                                                (isBrief == null || !isBrief.Equals("yes"))) // Completed full
                                    {
                                        toReturn = games[GameID].GameStatus;
                                    }
                                }
                        
                                //update server
                                SetStatus(OK);
                                return toReturn;
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to check if a given word has been played given a game and a user.
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="gameID"></param>
        /// <param name="targetWord"></param>
        /// <returns></returns>
        private bool HasBeenPlayed(string userToken, string gameID, string targetWord)
        {
            using (SqlConnection conn = new SqlConnection(BoggleServiceDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand("select Word from Words where Word = @Word", conn, trans))
                    {
                        command.Parameters.AddWithValue("@Word", targetWord);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                SetStatus(Forbidden);
                                trans.Commit();
                                return false;
                            }
                        }
                    }

                    String query = "select GameID, Word from Words, userToken where Words.Player = @userToken and GameID where Words.GameID = @GameID";
                    using (SqlCommand cmd = new SqlCommand(query, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@userToken", userToken);
                        cmd.Parameters.AddWithValue("@GameID", gameID);

                        IList<string> tempList = new List<string>();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {

                            while (reader.Read())
                            {
                                tempList.Add((string)reader["Word"]);
                            }
                        }

                        if (tempList.Contains(targetWord))
                        {
                            trans.Commit();
                            return true;
                        }
                        else
                        {
                            trans.Commit();
                            return false;
                        }
                    }
                }
            }
        }
    }
}
