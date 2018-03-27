using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Boggle
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

    /// <summary>
    /// Used in Game Status request.
    /// </summary>
    public class GameStatus
    {
        /// <summary>
        /// String for current GameState
        /// </summary>
        public string GameState { get; set; }

        /// <summary>
        /// String for current Board
        /// </summary>
        public string Board { get; set; }

        /// <summary>
        /// Starting TimeLimit
        /// </summary>
        public int TimeLimit { get; set; }

        /// <summary>
        /// Time left in current game.
        /// </summary>
        public int TimeLeft { get; set; }
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
        public WordScore[] WordsPlayer { get; set; }
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

    public class UserInfo
    {
        public string Nickname { get; set; }

        public string UserToken { get; set; }

        public string GameStatus { get; set; }
    }
}