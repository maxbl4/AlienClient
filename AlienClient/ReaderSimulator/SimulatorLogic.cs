﻿using System;
using System.Collections.Generic;
using System.Linq;
using AlienClient.Ext;

namespace AlienClient.ReaderSimulator
{
    public class SimulatorLogic
    {
        private SimulatorLogicState state = SimulatorLogicState.WaitForLogin;
        private string login;
        private readonly Dictionary<string, string> propeties = new Dictionary<string, string>();

        public bool KeepaliveEnabled { get; set; } = true;

        public string HandleCommand(string command)
        {
            if (!command.StartsWith("\x1"))
                throw new NotSupportedException("Only handle commands with disabled prompt");
            command = command.Substring(1);
            switch (state)
            {
                case SimulatorLogicState.WaitForLogin:
                    login = command;
                    state = SimulatorLogicState.WaitForPassword;
                    return "";
                case SimulatorLogicState.WaitForPassword:
                    if (login != "alien" || command != "password")
                    {
                        state = SimulatorLogicState.WaitForLogin;
                        return ProtocolMessages.InvalidUserNameOrPassword;
                    }
                    state = SimulatorLogicState.Ready;
                    return "";
                case SimulatorLogicState.Ready:
                    command = command.Trim();
                    if (command.EndsWith("?"))
                        return GetProperty(command);
                    if (command.Contains("="))
                        return SetProperty(command);
                    return ExecuteAction(command);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private string ExecuteAction(string command)
        {
            switch (command.ToLowerInvariant())
            {
                case "":  //keepalive
                    if (KeepaliveEnabled)
                        return "";
                    return null;
                case "clear":
                    return ProtocolMessages.TagListClearConfirmation;
                case "automodereset":
                    return ProtocolMessages.AutoModeResetConfirmation;
                default:
                    return ProtocolMessages.CommandNotUnderstood;
            }
        }

        private string SetProperty(string command)
        {
            var kv = command.Split(new[] {'='}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            if (kv.Count != 2)
                return ProtocolMessages.InvalidUseOfCommand;
            if (ReadonlyProperty(kv[0], out var ret))
                return ret;
            var key = kv[0].ToLowerInvariant();
            propeties[key] = kv[1];
            return command;
        }

        private string GetProperty(string command)
        {
            command = command.Substring(0, command.Length - 1);
            if (ReadonlyProperty(command, out var ret))
                return ret;
            var key = command.ToLowerInvariant();
            if (propeties.ContainsKey(key))
                return $"{command} = {propeties[key]}";
            return ProtocolMessages.InvalidUseOfCommand;;
        }

        private bool ReadonlyProperty(string command, out string response)
        {
            switch (command.ToLowerInvariant())
            {
                case "taglist":
                    response = propeties.Get("antennasequence") == "0" ? ProtocolMessages.KnownTags : ProtocolMessages.NoTags;
                    break;
                default:
                    response = null;
                    return false;
            }

            return true;
        }
    }
}