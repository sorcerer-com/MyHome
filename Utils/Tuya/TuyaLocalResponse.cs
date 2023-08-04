﻿namespace MyHome.Utils.Tuya;

/// <summary>
/// Response from local Tuya device.
/// </summary>
public class TuyaLocalResponse
{
    /// <summary>
    /// Command code.
    /// </summary>
    public TuyaCommand Command { get; }
    /// <summary>
    /// Return code.
    /// </summary>
    public int ReturnCode { get; }
    /// <summary>
    /// Response as JSON string.
    /// </summary>
    public string JSON { get; }

    internal TuyaLocalResponse(TuyaCommand command, int returnCode, string json)
    {
        this.Command = command;
        this.ReturnCode = returnCode;
        this.JSON = json;
    }

    public override string ToString()
    {
        return $"{this.Command}: {this.JSON} (return code = {this.ReturnCode})";
    }
}