﻿namespace AlienClient.ReaderSimulator
{
    public class ProtocolMessages
    {
        public static string Welcome =
            "***********************************************\r\n" +
            "*\r\n" +
            "* Alien Technology : RFID Reader \r\n" +
            "*\r\n" +
            "***********************************************\r\n" +
            "\r\n" +
            "Username";

        public static string Username = "Username>";
        public static string Password = "Password>";
        public static string AlienPrompt = "Alien>";
        public static string InvalidUserNameOrPassword = "Error: Invalid Username and/or Password";
        public static string AutoModeResetConfirmation = "All auto-mode settings have been reset!";
        public static string TagListClearConfirmation = "Tag List has been cleared!";
        public static string NoTags = "(No Tags)";
        public static string Goodbye = "Goodbye!";
        public static string CommandNotUnderstood = "Error 1: Command not understood.";
        public static string InvalidUseOfCommand = "Error 4: Invalid use of command.";

        public static string KnownTags = 
            "E20000165919004418405CBA\r\n" +
            "E20000165919006718405C92\r\n" +
            "E20000165919007818405C7B\r\n" +
            "E20000165919007718405C83\r\n" +
            "E20000165919006518405C91";
    }
}