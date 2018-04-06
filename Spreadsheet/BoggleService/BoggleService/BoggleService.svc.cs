using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.ServiceModel.Web;
using static System.Net.HttpStatusCode;

/// <summary>
/// This namespace acts as the logic for a server that users can connect to, in order to play other users in a Bogglegame. It implements
/// IBoggleService, and uses BoggleBoard for the board layout.
/// 
/// Authors: Bryce Hansen, Chris Hanson.
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

        static BoggleService()
        {
            // Saves the connection string for the database.  A connection string contains the
            // information necessary to connect with the database server.  When you create a
            // DB, there is generally a way to obtain the connection string.  From the Server
            // Explorer pane, obtain the properties of DB to see the connection string.

            // The connection string of my ToDoDB.mdf shows as
            //
            //    Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename="C:\Users\zachary\Source\CS 3500 S16\examples\ToDoList\ToDoListDB\App_Data\ToDoDB.mdf";Integrated Security=True
            //
            // Unfortunately, this is absolute pathname on my computer, which means that it
            // won't work if the solution is moved.  Fortunately, it can be shorted to
            //
            //    Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename="|DataDirectory|\ToDoDB.mdf";Integrated Security=True
            //
            // You should shorten yours this way as well.
            //
            // Rather than build the connection string into the program, I store it in the Web.config
            // file where it can be easily found and changed.  You should do that too.
            BoggleServiceDB = ConfigurationManager.ConnectionStrings["BoggleDB"].ConnectionString;
        }

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
            if (dictionaryWords.Count == 0)
            {
                // The first time a user registers to the server, copy contents of .txt file into HashSet for const. access
                string line;
                using (StreamReader file = new System.IO.StreamReader(AppDomain.CurrentDomain.BaseDirectory + "dictionary.txt"))
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
                        command.Parameters.AddWithValue("@Nickname", name.Nickname.Trim());

                        // This executes the command within the transaction over the connection.  The number of rows
                        // that were modified is returned.  Perhaps I should check and make sure that 1 is returned
                        // as expected.
                        command.ExecuteNonQuery();
                        SetStatus(Created);

                        // Immediately before each return that appears within the scope of a transaction, it is
                        // important to commit the transaction.  Otherwise, the transaction will be aborted and
                        // rolled back as soon as control leaves the scope of the transaction. 
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
        public GameIDOnly Join(TokenTime tkTime)
        {
            using (SqlConnection conn = new SqlConnection(BoggleServiceDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    int gameTemp = 0;
                    // Does user exist? Also check time limit
                    using (SqlCommand command = new SqlCommand("select UserToken from Users where UserToken = @UserToken", conn, trans))
                    {
                        command.Parameters.AddWithValue("@UserToken", tkTime.UserToken);
                        //must be a valid UserToken
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.HasRows || tkTime.TimeLimit < 5 || tkTime.TimeLimit > 120)
                            {
                                SetStatus(Forbidden);
                                reader.Close();
                                trans.Commit();
                                return null;
                            }
                        }
                    }
                    // Is the user already pending?
                    using (SqlCommand command = new SqlCommand("select Player1, GameID from Games where Player2 is NULL", conn, trans))

                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {

                            if (reader.HasRows && reader.Read()) // there is a pending game
                            {
                                string tempToken = (string)reader["Player1"];
                                gameTemp = (int) reader["GameID"];

                                    if (tempToken.Equals(tkTime.UserToken))  // This user is already pending
                                    {
                                        SetStatus(Conflict);
                                        reader.Close();
                                        trans.Commit();
                                        return null;
                                    }
                            }
                            else if (!reader.HasRows) // No pending game
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
                                    SetStatus(Accepted);
                                    reader.Close();
                                    trans.Commit();
                                    return gameIDReturn;
                                }
                            }
                        }
                    }
                    // Second player found, match begins
                    using (SqlCommand command = 
                        new SqlCommand("update Games " +
                            "set Player2 = @Player2, TimeLimit = @TimeLimit, StartTime = @StartTime " +
                            "where Player2 is null", conn, trans))
                    {
                        int? newTLimit;
                        command.Parameters.AddWithValue("@Player2", tkTime.UserToken);
                        using (SqlCommand selectPrevTimeLimit = new SqlCommand("Select TimeLimit from Games where Player2 is NULL", conn, trans))
                        { // CHECK the command above; IT IS PROBABLY NOT CORRECT
                            using (SqlDataReader reader = selectPrevTimeLimit.ExecuteReader())
                            {
                                reader.Read(); // Maybe unnecessary
                                int oldTimeLimit = reader.GetInt32(0);
                                newTLimit = (tkTime.TimeLimit + oldTimeLimit) / 2;
                            }
                        }
                        command.Parameters.AddWithValue("@TimeLimit", newTLimit);
                        command.Parameters.AddWithValue("@StartTime", DateTime.Now);
                        command.ExecuteNonQuery();

                        GameIDOnly gameIDReturn = new GameIDOnly();
                        gameIDReturn.GameID = gameTemp.ToString();
                        SetStatus(Created);
                        gameIsPending = false;

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
        public void CancelJoin(Token userTkn)
        {
            using (SqlConnection conn = new SqlConnection(BoggleServiceDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand deleteCmd = new SqlCommand("delete from Games where Player1 = @Player1 and Player2 is NULL", conn, trans))
                    {
                        deleteCmd.Parameters.AddWithValue("@Player1", userTkn.UserToken);

                        if (deleteCmd.ExecuteNonQuery() == 0)
                        {
                            SetStatus(Forbidden);
                        }
                        else
                        {
                            SetStatus(OK);
                        }
                        trans.Commit();
                    }
                }
            }

            //lock (sync)
            //{
            //    //must be a user that is pending finding a game.
            //    if (!users.ContainsKey(userTkn.UserToken) ||
            //        !userTkn.UserToken.Equals(games[pendingGameID].Player1Token))
            //    {
            //        SetStatus(Forbidden);
            //    }
            //    else if (gameIsPending && userTkn.UserToken.Equals(games[pendingGameID].Player1Token))
            //    {
            //        // Remove pending game
            //        games.Remove(pendingGameID);
            //        pendingGameID = null;
            //        gameIsPending = false;
            //        SetStatus(OK);
            //    }
            //}
        }

        /// <summary>
        /// Plays a word in specified game of Boggle
        /// </summary>
        /// <param name="wordToPlay">Contains UserToken and Word</param>
        /// <param name="gameID">The GameID of target game</param>
        public ScoreOnly PlayWord(TokenWord wordToPlay, string gameID)
        {
            if (wordToPlay.Word == null || wordToPlay.Word.Equals("") || wordToPlay.Word.Trim().Length > 30)
            { // Forbidden conditions not requireing DB access
                SetStatus(Forbidden);
                return null;
            }

            using (SqlConnection conn = new SqlConnection(BoggleServiceDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand selectGamesCmd = new SqlCommand("select Player2, Board from Games " +
                        "where (Player1 = @UserToken or Player2 = @UserToken) and GameID = @GameID", conn, trans))
                    {
                        selectGamesCmd.Parameters.AddWithValue("@UserToken", wordToPlay.UserToken);
                        selectGamesCmd.Parameters.AddWithValue("@GameID", gameID);
                        using (SqlDataReader reader = selectGamesCmd.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                SetStatus(Forbidden);
                                reader.Close();
                                trans.Commit();
                                return null;
                            }
                            // if game isn't active, update server with a conflict status
                            else if (reader.Read() && reader.GetValue(0) == null)
                            {
                                SetStatus(Conflict);
                                reader.Close();
                                trans.Commit();
                                return null;
                            }
                            else // word will be successfully played
                            {
                                string boardStr = (string)reader.GetValue(1);
                                using (SqlCommand command = new SqlCommand("insert into Words (Word, GameID, Player, Score) " +
                                    "values(@Word, @GameID, @Player, @Score)", conn, trans))
                                {
                                    //trim and save word with token
                                    string theWord = wordToPlay.Word.Trim().ToUpper();
                                    string theToken = wordToPlay.UserToken;
                                    ScoreOnly scoreToReturn = new ScoreOnly();
                                    int tempScore = tempScore = 0;
                                    BoggleBoard tempBoard = new BoggleBoard(boardStr);

                                    //update scores according to word length
                                    if (tempBoard.CanBeFormed(theWord) && dictionaryWords.Contains(theWord) &&
                                        !HasBeenPlayed(wordToPlay.UserToken, gameID, wordToPlay.Word))
                                    {
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
                                    else // Invalid word played
                                    {
                                        if (theWord.Length > 2)
                                        {
                                            tempScore = -1;
                                        }
                                    }
                                    //add to words played and increment point
                                    command.Parameters.AddWithValue("@Word", wordToPlay.Word);
                                    command.Parameters.AddWithValue("@GameID", gameID);
                                    command.Parameters.AddWithValue("@Player", wordToPlay.UserToken);
                                    command.Parameters.AddWithValue("@Score", tempScore);

                                    SetStatus(OK);
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
                                reader.Close();
//                                trans.Commit();
                                return null;
                            }
                        }
                    }

                    int startTime;
                    string p2name;

                    using (SqlCommand command = new SqlCommand("select Player1, Player2, Board, TimeLimit, StartTime " +
                        "from Games where GameID = @GameID", conn, trans))
                    {
                        command.Parameters.AddWithValue("@GameID", GameID);
                        GameStatus toReturn = new GameStatus();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            reader.Read();
                            toReturn.Board = (string) reader["Board"];
//                            toReturn.TimeLeft = (int) reader["TimeLeft"];
                            toReturn.TimeLimit = (int) reader["TimeLimit"];

                            using (SqlCommand playerStatus =
                                new SqlCommand("select Word, GameID, Player, Score from Words " +
                                "where Words.GameID = @GameID and Words.Player = @Player", conn, trans))
                            {
                                //this block extracts nicknames from games user tokens.
                                String p1name = (reader["Player1"]).ToString();
                                p2name = (reader["Player2"]).ToString();

                                if (p2name.Equals(""))
                                    p2name = null;

                                using (SqlCommand player1Nickname = new SqlCommand("select Nickname, UserToken from Users " +
                                    "where Users.UserToken = @UserToken", conn, trans))
                                {
                                    player1Nickname.Parameters.AddWithValue("@UserToken", p1name);
                                    using (SqlDataReader nameReader = player1Nickname.ExecuteReader())
                                    {
                                        nameReader.Read();
                                        p1name = (string) nameReader["Nickname"];
                                    }
                                }

                                if (p2name != null)
                                using (SqlCommand player2Nickname = new SqlCommand("select Nickname, UserToken from Users " +
                                    "where Users.UserToken = @UserToken", conn, trans))
                                {
                                    player2Nickname.Parameters.AddWithValue("@UserToken", p2name);
                                    using (SqlDataReader nameReader = player2Nickname.ExecuteReader())
                                    {
                                        nameReader.Read();
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

                                if (p2name != null)
                                {
                                    using (SqlCommand playerStatus2 =
                                        new SqlCommand("select Word, GameID, Player, Score from Words " +
                                                       "where Words.GameID = @GameID and Words.Player = @Player", conn,
                                            trans))
                                    {
                                        playerStatus2.Parameters.AddWithValue("@GameID", GameID);
                                        playerStatus2.Parameters.AddWithValue("@Player", p2name);
                                        using (SqlDataReader wordAndScoreP2 = playerStatus2.ExecuteReader())
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

                            if (p2name != null)
                            {
                                DateTime temp = (DateTime) reader["StartTime"];
                                startTime = temp.Millisecond;
                            }
                            else
                            {
                                startTime = DateTime.Now.Millisecond;
                            }
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
                                }
                        
                                //update server
                                trans.Commit();
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
                            reader.Read();
                            if (!reader.HasRows)
                            {
                                SetStatus(Forbidden);
                                reader.Close();
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
                            reader.Read();
                            while (reader.Read())
                            {
                                tempList.Add((string)reader["Word"]);
                            }
                            reader.Close();
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
