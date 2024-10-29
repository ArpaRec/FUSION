﻿using LabFusion.Extensions;
using LabFusion.Player;

using LabFusion.SDK.Metadata;

using Random = UnityEngine.Random;

namespace LabFusion.SDK.Gamemodes;

public class TeamManager
{
    private Gamemode _gamemode = null;
    public Gamemode Gamemode => _gamemode;

    private readonly HashSet<Team> _teams = new();
    public HashSet<Team> Teams => _teams;

    public event Action<PlayerId, Team> OnAssignedToTeam, OnRemovedFromTeam;

    private readonly Dictionary<PlayerId, MetadataVariable> _playersToTeam = new();

    /// <summary>
    /// Registers the TeamManager to a gamemode. This is required for events to be processed properly.
    /// </summary>
    /// <param name="gamemode">The gamemode to register to.</param>
    public void Register(Gamemode gamemode)
    {
        _gamemode = gamemode;
        gamemode.Metadata.OnMetadataChanged += OnMetadataChanged;
        gamemode.Metadata.OnMetadataRemoved += OnMetadataRemoved;
    }

    /// <summary>
    /// Unregisters the TeamManager. Make sure to call this when the TeamManager will no longer be used.
    /// </summary>
    public void Unregister()
    {
        _gamemode.Metadata.OnMetadataChanged -= OnMetadataChanged;
        _gamemode.Metadata.OnMetadataRemoved -= OnMetadataRemoved;
        _gamemode = null;
    }

    private void OnMetadataChanged(string key, string value)
    {
        // Check if this is a team key
        if (!KeyHelper.KeyMatchesVariable(key, CommonKeys.TeamKey))
        {
            return;
        }

        var player = KeyHelper.GetPlayerFromKey(key);

        var teamVariable = new MetadataVariable(key, Gamemode.Metadata);

        _playersToTeam[player] = teamVariable;

        // Remove from existing teams
        foreach (var existingTeam in Teams)
        {
            if (existingTeam.HasPlayer(player))
            {
                OnRemovedFromTeam?.Invoke(player, existingTeam);
                existingTeam.ForceRemovePlayer(player);
            }
        }

        // Invoke team change event
        var team = GetTeamByName(value);
        
        if (team != null)
        {
            OnAssignedToTeam?.Invoke(player, team);
            team.ForceAddPlayer(player);
        }
    }

    private void OnMetadataRemoved(string key, string value)
    {
        // Check if this is a team key
        if (!KeyHelper.KeyMatchesVariable(key, CommonKeys.TeamKey))
        {
            return;
        }

        var player = KeyHelper.GetPlayerFromKey(key);

        _playersToTeam.Remove(player);

        // Invoke team remove event
        var team = GetTeamByName(value);

        if (team != null)
        {
            OnRemovedFromTeam?.Invoke(player, team);
            team.ForceRemovePlayer(player);
        }
    }

    /// <summary>
    /// Adds a new team.
    /// </summary>
    /// <param name="team">The team to add.</param>
    public void AddTeam(Team team)
    {
        _teams.Add(team);
    }

    /// <summary>
    /// Removes a registered team.
    /// </summary>
    /// <param name="team"></param>
    public void RemoveTeam(Team team)
    {
        _teams.Remove(team);
    }

    /// <summary>
    /// Unassigns players from every team, and then removes all registered teams.
    /// </summary>
    public void ClearTeams()
    {
        UnassignAllPlayers();

        foreach (var team in _teams.ToArray())
        {
            RemoveTeam(team);
        }
    }

    /// <summary>
    /// Tries assigning a player to a team.
    /// </summary>
    /// <param name="player">The player to assign to <paramref name="team"/>.</param>
    /// <param name="team">The team that <paramref name="player"/> will be assigned to.</param>
    /// <returns>Whether the assign was successful.</returns>
    public bool TryAssignTeam(PlayerId player, Team team)
    {
        var playerKey = KeyHelper.GetKeyFromPlayer(CommonKeys.TeamKey, player);
        return Gamemode.Metadata.TrySetMetadata(playerKey, team.TeamName);
    }

    /// <summary>
    /// Assigns every player to random teams.
    /// <para>Evenly distributes the players among the teams to the best of its ability.</para>
    /// </summary>
    public void AssignToRandomTeams()
    {
        // Shuffle the players for randomness
        var players = new List<PlayerId>(PlayerIdManager.PlayerIds);
        players.Shuffle();

        // Iterate and assign teams
        int teamIndex = 0;
        int teamCount = Teams.Count;

        foreach (var player in players)
        {
            if (teamIndex >= teamCount)
            {
                teamIndex = 0;
            }

            TryAssignTeam(player, Teams.ElementAt(teamCount++));
        }
    }

    /// <summary>
    /// Assigns a player to the smallest team.
    /// <para>IMPORTANT: This should NOT be used for assigning teams to players on gamemode start!</para>
    /// <para>When assigning teams, the messages have not yet been received, meaning this can keep returning the same team!</para>
    /// <para>Instead, use <see cref="AssignToRandomTeams"/> for this purpose, and this on late joins.</para>
    /// </summary>
    /// <param name="player"></param>
    public void AssignToSmallestTeam(PlayerId player)
    {
        TryAssignTeam(player, GetTeamWithFewestPlayers());
    }

    /// <summary>
    /// Tries unassigning a player from their team.
    /// </summary>
    /// <param name="player"></param>
    /// <returns>Whether the unassign was successful.</returns>
    public bool TryUnassignTeam(PlayerId player)
    {
        var playerKey = KeyHelper.GetKeyFromPlayer(CommonKeys.TeamKey, player);
        return Gamemode.Metadata.TryRemoveMetadata(playerKey);
    }

    /// <summary>
    /// Unassigns every player from a team.
    /// </summary>
    public void UnassignAllPlayers()
    {
        foreach (var player in PlayerIdManager.PlayerIds)
        {
            TryUnassignTeam(player);
        }
    }

    /// <summary>
    /// Gets a team by its <see cref="Team.TeamName"/>, or null if there is not one found.
    /// </summary>
    /// <param name="name"></param>
    /// <returns>The team named <paramref name="name"/>.</returns>
    public Team GetTeamByName(string name)
    {
        foreach (var team in Teams)
        {
            if (team.TeamName == name)
            {
                return team;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the given team of a player, or null if they have not been assigned one.
    /// </summary>
    /// <param name="player"></param>
    /// <returns>The given team of a player.</returns>
    public Team GetPlayerTeam(PlayerId player)
    {
        if (!_playersToTeam.TryGetValue(player, out var teamVariable))
        {
            return null;
        }

        return GetTeamByName(teamVariable.GetValue());
    }

    /// <summary>
    /// Gets the local player's team.
    /// </summary>
    /// <returns>The local player's team.</returns>
    public Team GetLocalTeam()
    {
        return GetPlayerTeam(PlayerIdManager.LocalId);
    }
    
    /// <summary>
    /// Gets a random team.
    /// </summary>
    /// <returns>A randomly selected team.</returns>
    public Team GetRandomTeam()
    {
        return Teams.ElementAt(Random.RandomRangeInt(0, Teams.Count));
    }

    /// <summary>
    /// Gets the team with the fewest registered players.
    /// <para>IMPORTANT: This should NOT be used for assigning teams to players on gamemode start!</para>
    /// <para>When assigning teams, the messages have not yet been received, meaning this can keep returning the same team!</para>
    /// <para>Instead, use <see cref="AssignToRandomTeams"/> for this purpose, and <see cref="AssignToSmallestTeam(PlayerId)"/> on late joins.</para>
    /// </summary>
    /// <returns>The team with fewest players.</returns>
    public Team GetTeamWithFewestPlayers()
    {
        int lowestPlayers = int.MaxValue;
        Team lowestTeam = null;

        foreach (var team in Teams)
        {
            if (team.PlayerCount < lowestPlayers)
            {
                lowestPlayers = team.PlayerCount;
                lowestTeam = team;
            }
        }

        return lowestTeam;
    }
}
