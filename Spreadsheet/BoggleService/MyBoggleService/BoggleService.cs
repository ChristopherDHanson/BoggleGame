using CustomNetworking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Text;
using static System.Net.HttpStatusCode;

/// <summary>
/// This namespace acts as the logic for a server that users can connect to, in order to play other users in a Bogglegame. It implements
/// IBoggleService, and uses BoggleBoard for the board layout.
/// 
/// Authors: Bryce Hansen, Chris Hanson. 4/5/18
/// </summary>
namespace MyBoggleService
{
    public class BoggleService
    {
        //holds the dictionary of valid and playable words.
        private static HashSet<string> dictionaryWords = new HashSet<string>(); // words that are valid inputs
        private static string BoggleServiceDB;

        static BoggleService()
        {
            // Saves the connection string for the database.  A connection string contains the
            // information necessary to connect with the database server.  When you create a
            // DB, there is generally a way to obtain the connection string.  From the Server
            // Explorer pane, obtain the properties of DB to see the connection string.

            // Connection string stored in Web.config
            //BoggleServiceDB = ConfigurationManager.ConnectionStrings["BoggleDB"].ConnectionString;
            BoggleServiceDB = @"Data Source = (LocalDB)\MSSQLLocalDB; AttachDbFilename = |DataDirectory|\BoggleDB.mdf; Integrated Security = True; MultipleActiveResultSets= True ";
        }

        ///// <summary>
        ///// The most recent call to SetStatus determines the response code used when
        ///// an http response is sent.
        ///// </summary>
        ///// <param name="status"></param>
        //private static void SetStatus(string theStatus)
        //{
        //    //WebOperationContext.Current.OutgoingResponse.StatusCode = status;
        //    status = theStatus;
        //}

        /// <summary>
        /// Returns a Stream version of index.html.
        /// </summary>
        /// <returns></returns>
        public Stream API(out HttpStatusCode status)
        {
            //SetStatus("OK");
            status = OK;
            //WebOperationContext.Current.OutgoingResponse.ContentType = "text/html";
            return File.OpenRead(AppDomain.CurrentDomain.BaseDirectory + "index.html");
        }

        /// <summary>
        /// Register new user
        /// </summary>
        /// <param name="name">Contains Nickname</param>
        /// <returns>Returns user token</returns>
        public Token Register(UserName name, out HttpStatusCode status)
        {
            if (dictionaryWords.Count == 0)
            {
                // The first time a user registers to the server, copy contents of .txt file into HashSet for const. access
                string line;
                using (StreamReader file = new System.IO.StreamReader("dictionary.txt"))
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
                //SetStatus("Forbidden");
                status = Forbidden;
                return null;
            }
            theName = theName.Trim();
            if (theName.Length == 0 || theName.Length > 50)
            {
                //SetStatus("Forbidden");
                status = Forbidden;
                return null;
            }

            // Otherwise, insert the name
            using (SqlConnection conn = new SqlConnection(BoggleServiceDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command =
                        new SqlCommand("insert into Users (UserToken, Nickname) values(@UserToken, @Nickname)", conn, trans))
                    {
                        string newUserToken = Guid.NewGuid().ToString();

                        command.Parameters.AddWithValue("@UserToken", newUserToken);
                        command.Parameters.AddWithValue("@Nickname", theName);
                        command.ExecuteNonQuery();
                        //SetStatus("Created");
                        status = Created;
                        trans.Commit();
                        Token returnToke = new Token();
                        returnToke.UserToken = newUserToken;
                        return returnToke;
                    }
                }
            }
        }

        /// <summary>
        /// Join new game
        /// </summary>
        /// <param name="tkTime">Contains UserToken and desired TimeLimit</param>
        /// <returns>Returns new GameID</returns>
        public GameIDOnly Join(TokenTime tkTime, out HttpStatusCode status)
        {
            using (SqlConnection conn = new SqlConnection(BoggleServiceDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    int gameIDTemp = 0;
                    // Check if user is registered, and if time limit is valid
                    using (SqlCommand command = new SqlCommand("select UserToken from Users where UserToken = @UserToken", conn, trans))
                    {
                        command.Parameters.AddWithValue("@UserToken", tkTime.UserToken);
                        using (SqlDataReader reader = command.ExecuteReader())
                        { // If UserToken is valid
                            if (!reader.HasRows || tkTime.TimeLimit < 5 || tkTime.TimeLimit > 120)
                            {
                                //SetStatus("Forbidden");
                                status = Forbidden;
                                reader.Close();
                                trans.Commit();
                                return null;
                            }
                        }
                    }
                    // Check if user is already in a pending game
                    using (SqlCommand command = new SqlCommand("select Player1, GameID from Games where Player2 is NULL", conn, trans))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows && reader.Read()) // If there is a pending game
                            {
                                gameIDTemp = (int)reader.GetValue(1); // Get GameID of pending game
                                if (((string)reader[0]).Equals(tkTime.UserToken)) // If this user is already pending
                                {
                                    //SetStatus("Conflict");
                                    status = Conflict;
                                    reader.Close();
                                    trans.Commit();
                                    return null;
                                }
                            }
                            else if (!reader.HasRows) // Else if there is a pending game
                            {
                                using (SqlCommand insertCmd = new SqlCommand("insert into Games (Player1, Board, TimeLimit) " +
                                    "output inserted.GameID values(@Player1, @Board, @TimeLimit)", conn, trans))
                                {
                                    insertCmd.Parameters.AddWithValue("@Player1", tkTime.UserToken);
                                    BoggleBoard tempBBoard = new BoggleBoard();
                                    insertCmd.Parameters.AddWithValue("@Board", tempBBoard.ToString());
                                    insertCmd.Parameters.AddWithValue("@TimeLimit", tkTime.TimeLimit);

                                    GameIDOnly gameIDReturn = new GameIDOnly();
                                    gameIDReturn.GameID = insertCmd.ExecuteScalar().ToString();
                                    //SetStatus("Accepted");
                                    status = Accepted;
                                    reader.Close();
                                    trans.Commit();
                                    return gameIDReturn;
                                }
                            }
                        }
                    }
                    // Else, a second player has been found, match begins
                    using (SqlCommand command =
                        new SqlCommand("update Games " +
                            "set Player2 = @Player2, TimeLimit = @TimeLimit, StartTime = @StartTime " +
                            "where Player2 is NULL", conn, trans))
                    {
                        int? newTLimit;
                        using (SqlCommand selectPrevTimeLimit = new SqlCommand("select TimeLimit from Games where Player2 is null", conn, trans))
                        { // Calculate the new time limit
                            using (SqlDataReader reader = selectPrevTimeLimit.ExecuteReader())
                            {
                                reader.Read();
                                int oldTimeLimit = reader.GetInt32(0);
                                newTLimit = (tkTime.TimeLimit + oldTimeLimit) / 2;
                            }
                        }

                        command.Parameters.AddWithValue("@Player2", tkTime.UserToken);
                        command.Parameters.AddWithValue("@TimeLimit", newTLimit);
                        command.Parameters.AddWithValue("@StartTime", DateTime.Now); // Start the game by setting StartTime to now
                        command.ExecuteNonQuery();

                        GameIDOnly gameIDReturn = new GameIDOnly();
                        gameIDReturn.GameID = gameIDTemp.ToString();
                        //SetStatus("Created");
                        status = Created;
                        trans.Commit();
                        return gameIDReturn;
                    }
                }
            }
        }

        /// <summary>
        /// Cancels Join request if user is still in pending game
        /// </summary>
        /// <param name="userTkn">Contains UserToken</param>
        public void CancelJoin(Token userTkn, out HttpStatusCode status)
        {
            using (SqlConnection conn = new SqlConnection(BoggleServiceDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand deleteCmd = new SqlCommand("delete from Games where Player1 = @Player1 and Player2 is null", conn, trans))
                    {
                        deleteCmd.Parameters.AddWithValue("@Player1", userTkn.UserToken);
                        if (deleteCmd.ExecuteNonQuery() == 0) // If user is not in a pending game, Forbidden status
                        {
                            //SetStatus("Forbidden");
                            status = Forbidden;
                        }
                        else
                        {
                            //SetStatus("OK");
                            status = OK;
                        }
                        trans.Commit();
                    }
                }
            }
        }

        /// <summary>
        /// Plays a word in specified game of Boggle
        /// </summary>
        /// <param name="wordToPlay">Contains UserToken and Word</param>
        /// <param name="gameID">The GameID of target game</param>
        public ScoreOnly PlayWord(TokenWord wordToPlay, string gameID, out HttpStatusCode status)
        {
            if (wordToPlay.Word == null || wordToPlay.Word.Equals("") || wordToPlay.Word.Trim().Length > 30)
            { // Forbidden conditions not requiring DB access
                //SetStatus("Forbidden");
                status = Forbidden;
                return null;
            }

            using (SqlConnection conn = new SqlConnection(BoggleServiceDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand selectGamesCmd = new SqlCommand("select Player2, Board, TimeLimit, StartTime from Games " +
                        "where (Player1 = @UserToken or Player2 = @UserToken) and GameID = @GameID", conn, trans))
                    {
                        // Check here if word is valid
                        selectGamesCmd.Parameters.AddWithValue("@UserToken", wordToPlay.UserToken);
                        selectGamesCmd.Parameters.AddWithValue("@GameID", gameID);
                        using (SqlDataReader reader = selectGamesCmd.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                //SetStatus("Forbidden");
                                status = Forbidden;
                                reader.Close();
                                trans.Commit();
                                return null;
                            }

                            if (reader.Read()) // If game is not active, update server with Conflict status
                            {
                                if (reader.GetValue(0).ToString().Equals("")) // Pending
                                {
                                    //SetStatus("Conflict");
                                    status = Conflict;
                                    reader.Close();
                                    trans.Commit();
                                    return null;
                                }
                                DateTime tempStartTime = reader.GetDateTime(3);
                                if (DateTime.Now.Subtract(tempStartTime).TotalSeconds > reader.GetInt32(2)) // Completed
                                {
                                    //SetStatus("Conflict");
                                    status = Conflict;
                                    reader.Close();
                                    trans.Commit();
                                    return null;
                                }
                            }
                            // Otherwise, word will be successfully played
                            string boardStr = (string)reader.GetValue(1); // Get the Board string from SqlReader
                            using (SqlCommand command = new SqlCommand("insert into Words (Word, GameID, Player, Score) " +
                                "values(@Word, @GameID, @Player, @Score)", conn, trans))
                            {
                                // Trim and save word with token
                                string theWord = wordToPlay.Word.Trim().ToUpper();
                                string theToken = wordToPlay.UserToken;
                                ScoreOnly scoreToReturn = new ScoreOnly();
                                int tempScore = tempScore = 0;
                                BoggleBoard tempBoard = new BoggleBoard(boardStr);

                                if (tempBoard.CanBeFormed(theWord) && dictionaryWords.Contains(theWord) &&
                                    !HasBeenPlayed(wordToPlay.UserToken, gameID, wordToPlay.Word, out HttpStatusCode Ostatus)) // If valid
                                {
                                    status = Ostatus;
                                    // Update scores according to word length
                                    if (theWord.Length == 3 || theWord.Length == 4)
                                        tempScore = 1;
                                    else if (theWord.Length == 5)
                                        tempScore = 2;
                                    else if (theWord.Length == 6)
                                        tempScore = 3;
                                    else if (theWord.Length == 7)
                                        tempScore = 5;
                                    else if (theWord.Length > 7)
                                        tempScore = 11;
                                }
                                else // Else invalid word played
                                {
                                    if (theWord.Length > 2)
                                    {
                                        tempScore = -1;
                                    }
                                }
                                // Prepare command to add word to Words
                                command.Parameters.AddWithValue("@Word", wordToPlay.Word);
                                command.Parameters.AddWithValue("@GameID", gameID);
                                command.Parameters.AddWithValue("@Player", wordToPlay.UserToken);
                                command.Parameters.AddWithValue("@Score", tempScore);
                                command.ExecuteNonQuery();

                                //SetStatus("OK");
                                status = OK;
                                scoreToReturn.Score = tempScore;
                                reader.Close();
                                trans.Commit();
                                return scoreToReturn;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets and returns the GameStatus of specified game
        /// </summary>
        /// <param name="GameID">The GameID of target Game</param>
        /// <param name="isBrief">"yes" = brief, anything else = not brief</param>
        public GameStatus GetStatus(string GameID, string isBrief, out HttpStatusCode status)
        {
            using (SqlConnection conn = new SqlConnection(BoggleServiceDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand cmd = new SqlCommand("select GameID from Games where Games.GameID = @GameID", conn, trans))
                    {
                        // Check if GameID is valid. If not, Forbidden status
                        cmd.Parameters.AddWithValue("@GameID", GameID);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                //SetStatus("Forbidden");
                                status = Forbidden;
                                reader.Close();
                                //trans.Commit();
                                return null;
                            }
                        }
                    }

                    string p2name;

                    using (SqlCommand command = new SqlCommand("select Player1, Player2, Board, TimeLimit, StartTime " +
                        "from Games where Games.GameID = @GameID", conn, trans))
                    {
                        // Get all necessary info from game specified by GameID
                        command.Parameters.AddWithValue("@GameID", GameID);
                        GameStatus toReturn = new GameStatus();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            reader.Read();
                            toReturn.Board = (string)reader["Board"];
                            toReturn.TimeLimit = (int)reader["TimeLimit"];

                            using (SqlCommand playerStatus = new SqlCommand("select Word, GameID, Player, Score from Words " +
                                "where Words.GameID = @GameID and Words.Player = @Player", conn, trans))
                            {
                                // This block extracts nicknames from games user tokens.
                                String p1name = (reader["Player1"]).ToString();
                                p2name = (reader["Player2"]).ToString();
                                if (p2name.Equals("")) // If null in DB
                                    p2name = null;

                                using (SqlCommand player1Nickname = new SqlCommand("select Nickname, UserToken from Users " +
                                    "where Users.UserToken = @UserToken", conn, trans))
                                {
                                    // Get Nickname of p1
                                    player1Nickname.Parameters.AddWithValue("@UserToken", p1name);
                                    using (SqlDataReader nameReader = player1Nickname.ExecuteReader())
                                    {
                                        nameReader.Read();
                                        p1name = (string)nameReader["Nickname"];
                                    }
                                }

                                if (p2name != null)
                                    using (SqlCommand player2Nickname = new SqlCommand("select Nickname, UserToken from Users " +
                                        "where Users.UserToken = @UserToken", conn, trans))
                                    {
                                        // Get Nickname of p2
                                        player2Nickname.Parameters.AddWithValue("@UserToken", p2name);
                                        using (SqlDataReader nameReader = player2Nickname.ExecuteReader())
                                        {
                                            nameReader.Read();
                                            p2name = (string)nameReader["Nickname"];
                                        }
                                    }

                                // Extract word list for each player from current games played words. Add scores
                                int p1Score = 0;
                                int p2Score = 0;
                                IList<WordScore> p1WordList = new List<WordScore>();
                                IList<WordScore> p2WordList = new List<WordScore>();
                                playerStatus.Parameters.AddWithValue("@GameID", GameID);
                                playerStatus.Parameters.AddWithValue("@Player", (string)reader["Player1"]);
                                using (SqlDataReader wordAndScoreP1 = playerStatus.ExecuteReader())
                                {
                                    while (wordAndScoreP1.Read())
                                    {
                                        WordScore tempWS = new WordScore();
                                        tempWS.Word = (string)wordAndScoreP1["Word"];
                                        tempWS.Score = (int)wordAndScoreP1["Score"];
                                        p1WordList.Add(tempWS);
                                        p1Score += (int)wordAndScoreP1["Score"];
                                    }
                                }

                                if (p2name != null)
                                {
                                    using (SqlCommand playerStatus2 =
                                        new SqlCommand("select Word, GameID, Player, Score from Words " +
                                                       "where Words.GameID = @GameID and Words.Player = @Player", conn,
                                            trans))
                                    {
                                        playerStatus2.Parameters.AddWithValue("@GameID", GameID);
                                        playerStatus2.Parameters.AddWithValue("@Player", (string)reader["Player2"]);
                                        using (SqlDataReader wordAndScoreP2 = playerStatus2.ExecuteReader())
                                        {
                                            while (wordAndScoreP2.Read())
                                            {
                                                WordScore tempWS = new WordScore();
                                                tempWS.Word = (string)wordAndScoreP2["Word"];
                                                tempWS.Score = (int)wordAndScoreP2["Score"];
                                                p2WordList.Add(tempWS);
                                                p2Score += (int)wordAndScoreP2["Score"];
                                            }
                                        }
                                    }
                                }

                                toReturn.Player1 = (new PlayerStatus()
                                {
                                    Nickname = p1name,
                                    Score = p1Score,
                                    WordsPlayed = p1WordList
                                }); // Create new PlayerStatus obj from this token to info in Users table DB

                                toReturn.Player2 = (new PlayerStatus
                                {
                                    Nickname = p2name,
                                    Score = p2Score,
                                    WordsPlayed = p2WordList
                                }); // Create new PlayerStatus obj from this token to info in Users table DB
                            }
                        }



                        //Return logic goes here
                        //returns needed status update if pending, active, or completed and if isBrief == yes
                        if (toReturn.Player2.Nickname == null)
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
                            using (SqlCommand cmd = new SqlCommand(
                                "select StartTime from Games where Games.GameID = @GameID", conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@GameID", GameID);
                                using (SqlDataReader reader = cmd.ExecuteReader())
                                {
                                    reader.Read();
                                    //calculate time remaining to return
                                    DateTime timeNow = DateTime.Now;
                                    int? timeRemaining = (int)((double)toReturn.TimeLimit -
                                                            (timeNow.Subtract((DateTime)reader["StartTime"]).TotalSeconds));


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
                                    if (toReturn.Player2.Nickname != null &&
                                        (isBrief != null && isBrief.Equals("yes"))
                                    ) // Active or completed, brief response
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
                                    else if (toReturn.Player2.Nickname != null &&
                                                (isBrief != null && isBrief.Equals("no"))
                                    ) // Active or completed, brief response
                                    {
                                        if (!(timeRemaining <= 0))
                                            toReturn.GameState = "active";
                                        else
                                        {
                                            toReturn.GameState = "completed";
                                            toReturn.TimeLeft = 0;
                                        }
                                    }
                                }
                            }
                        }

                        // Update server
                        trans.Commit();
                        //SetStatus("OK");
                        status = OK;
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
        private bool HasBeenPlayed(string userToken, string gameID, string targetWord, out HttpStatusCode status)
        {
            using (SqlConnection conn = new SqlConnection(BoggleServiceDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand("select Word from Words where Word = @Word", conn, trans))
                    {
                        // See if Word is in Words (If it has ever been played)
                        command.Parameters.AddWithValue("@Word", targetWord);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            reader.Read();
                            if (!reader.HasRows)
                            {
                                //SetStatus("Forbidden");
                                status = Forbidden;
                                reader.Close();
                                trans.Commit();
                                return false;
                            }
                        }
                    }

                    String query = "select GameID, Word from Words where Words.Player = @userToken and Words.GameID = @GameID";
                    using (SqlCommand cmd = new SqlCommand(query, conn, trans))
                    {
                        // See if word has been played by this player in this game
                        cmd.Parameters.AddWithValue("@userToken", userToken);
                        cmd.Parameters.AddWithValue("@GameID", gameID);

                        IList<string> tempList = new List<string>();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                tempList.Add((string)reader["Word"]);
                            }
                            reader.Close();
                        }

                        if (tempList.Contains(targetWord))
                        {
                            status = OK;
                            trans.Commit();
                            return true;
                        }
                        else
                        {
                            status = OK;
                            trans.Commit();
                            return false;
                        }
                    }
                }
            }
        }
    }
}
