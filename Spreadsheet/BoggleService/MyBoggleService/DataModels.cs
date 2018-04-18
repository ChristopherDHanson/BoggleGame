using System.Collections.Generic;

/// <summary>
/// Contains variety of classes that represent objects that come in
/// the header of requests to server. Also contains classes representing
/// object types to be returned.
/// </summary>
namespace MyBoggleService
{
    /// <summary>
    /// Used in Create User request.
    /// </summary>
    public class UserName
    {
        /// <summary>
        /// Nickname of the user
        /// </summary>
        public string Nickname { get; set; }
    }

    /// <summary>
    /// Used in Join Game request.
    /// </summary>
    public class TokenTime
    {
        /// <summary>
        /// Token of user joining game
        /// </summary>
        public string UserToken { get; set; }

        /// <summary>
        /// Requested time limit
        /// </summary>
        public int TimeLimit { get; set; }
    }

    /// <summary>
    /// Used in Cancel Game request.
    /// </summary>
    public class Token
    {
        /// <summary>
        /// Token of user canceling join request
        /// </summary>
        public string UserToken { get; set; }
    }

    public class ScoreOnly
    {
        public int Score { get; set; }
    }

    /// <summary>
    /// Used in Play Word Request
    /// </summary>
    public class TokenWord
    {
        /// <summary>
        /// Token of user playing word
        /// </summary>
        public string UserToken { get; set; }

        /// <summary>
        /// Word being played
        /// </summary>
        public string Word { get; set; }
    }

    public class GameIDOnly
    {
        public string GameID { get; set; }
    }

    /// <summary>
    /// Used in Game Status request.
    /// </summary>
    public class GameStatus
    {
        /// <summary>
        /// Pending if looking for game. Active if in game. Completed if finished game.
        /// </summary>
        public string GameState { get; set; }

        /// <summary>
        /// String for current Board
        /// </summary>
        public string Board { get; set; }

        /// <summary>
        /// Starting TimeLimit
        /// </summary>
        public int? TimeLimit { get; set; }

        /// <summary>
        /// Time left in current game.
        /// </summary>
        public int? TimeLeft { get; set; }

        public PlayerStatus Player1 { get; set; }

        public PlayerStatus Player2 { get; set; }
    }

    /// <summary>
    /// Helper model for Player Status.
    /// </summary>
    public class PlayerStatus
    {
        /// <summary>
        /// Current players name
        /// </summary>
        public string Nickname { get; set; }

        /// <summary>
        /// Current players score.
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// Returns an array of WordScore pairs
        /// </summary>
        public IList<WordScore> WordsPlayed { get; set; }
    }

    /// <summary>
    /// Helper model for each Players word and score pairs.
    /// </summary>
    public class WordScore
    {
        /// <summary>
        /// Word played
        /// </summary>
        public string Word { get; set; }

        /// <summary>
        /// Value of word played.
        /// </summary>
        public int Score { get; set; }
    }

    /// <summary>
    /// Used to store data about games in dictionary in service class
    /// </summary>
    public class Game
    {
        public string Player1Token { get; set; }
        public string Player2Token { get; set; }
        public string GameID { get; set; }
        public BoggleBoard GameBoard { get; set; }
        public GameStatus GameStatus { get; set; }
        public int StartTime { get; set; }
    }
}