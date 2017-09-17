﻿using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Threading.Tasks;
using Botwinder.entities;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using guid = System.UInt64;

namespace Botwinder.core
{
	public partial class BotwinderClient<TUser> : IDisposable where TUser : UserData, new()
	{
		private Task InitCommands()
		{
			Command<TUser> newCommand = null;


// !global
			newCommand = new Command<TUser>("global");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Display all teh numbers.";
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				StringBuilder shards = new StringBuilder();
				Shard globalCount = new Shard();
				foreach( Shard shard in this.GlobalDb.Shards )
				{
					globalCount.ServerCount += shard.ServerCount;
					globalCount.UserCount += shard.UserCount;
					globalCount.MemoryUsed += shard.MemoryUsed;
					globalCount.ThreadsActive += shard.ThreadsActive;
					globalCount.MessagesTotal += shard.MessagesTotal;
					globalCount.MessagesPerMinute += shard.MessagesPerMinute;
					globalCount.OperationsRan += shard.OperationsRan;
					globalCount.OperationsActive += shard.OperationsActive;
					globalCount.Disconnects += shard.Disconnects;

					shards.AppendLine(shard.GetStatsString());
				}

				string message = "Server Status: <http://status.botwinder.info>\n\n" +
				                 $"Global Servers: `{globalCount.ServerCount}`\n" +
				                 $"Global Members `{globalCount.UserCount}`" +
				                 $"Global Allocated data Memory: `{globalCount.MemoryUsed} MB`\n" +
				                 $"Global Threads: `{globalCount.ThreadsActive}`\n" +
				                 $"Global Messages received: `{globalCount.MessagesTotal}`\n" +
				                 $"Global Messages per minute: `{globalCount.MessagesPerMinute}`\n" +
				                 $"Global Operations ran: `{globalCount.OperationsRan}`\n" +
				                 $"Global Operations active: `{globalCount.OperationsActive}`\n" +
				                 $"Global Disconnects: `{globalCount.Disconnects}`\n" +
				                 $"\n**Shards: `{shards.Length}`\n\n" +
				                 $"{shards.ToString()}";

				await SendMessageToChannel(e.Channel, message);
			};
			this.Commands.Add(newCommand.Id, newCommand);

// !getServer
			newCommand = new Command<TUser>("getServer");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Display some info about specific server with id/name, or owners id/username.";
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				guid id;
				StringBuilder response = new StringBuilder();
				IEnumerable<ServerStats> foundServers = null;
				if( string.IsNullOrEmpty(e.TrimmedMessage) ||
				    (!guid.TryParse(e.TrimmedMessage, out id) ||
				     !(foundServers = this.ServerDb.ServerStats.Where(s =>
					     s.ServerId == id || s.OwnerId == id || s.ServerName.Contains(e.TrimmedMessage) || s.OwnerName.Contains(e.TrimmedMessage)
				     )).Any()) )
				{
					await SendMessageToChannel(e.Channel, "Server not found.");
					return;
				}

				if( foundServers.Count() > 5 )
				{
					response.AppendLine("__**Found more than 5 servers!**__\n");
				}

				foreach(ServerStats server in foundServers.Take(5))
				{
					response.AppendLine(server.ToString());
					response.AppendLine();
				}

				await SendMessageToChannel(e.Channel, response.ToString());
			};
			this.Commands.Add(newCommand.Id, newCommand);

// !getInvite
			newCommand = new Command<TUser>("getInvite");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Get an invite url with serverid.";
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				guid id;
				ServerConfig foundServer = null;
				if( string.IsNullOrEmpty(e.TrimmedMessage) ||
				    !guid.TryParse(e.TrimmedMessage, out id) ||
				    (foundServer = this.ServerDb.ServerConfigurations.FirstOrDefault(s => s.ServerId == id)) == null )
				{
					await SendMessageToChannel(e.Channel, "Server not found.");
					return;
				}

				if( string.IsNullOrEmpty(foundServer.InviteUrl) )
				{
					await SendMessageToChannel(e.Channel, "I don't have permissions to create this InviteUrl.");
					return;
				}

				await SendMessageToChannel(e.Channel, foundServer.InviteUrl);
			};
			this.Commands.Add(newCommand.Id, newCommand);

// !maintenance
			newCommand = new Command<TUser>("maintenance");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Performe maintenance";
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e =>{
				await SendMessageToChannel(e.Channel, "Okay, this may take a while...");
				await LogMaintenanceAndExit();
			};
			this.Commands.Add(newCommand.Id, newCommand);

// !getExceptions
			newCommand = new Command<TUser>("getExceptions");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Get a list of exceptions.";
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				StringBuilder response = new StringBuilder();
				if( string.IsNullOrEmpty(e.TrimmedMessage) || !int.TryParse(e.TrimmedMessage, out int n) || n <= 0 )
					n = 10;

				foreach( ExceptionEntry exception in this.GlobalDb.Exceptions.Skip(Math.Max(0, this.GlobalDb.Exceptions.Count() - n)) )
				{
					response.AppendLine(exception.GetMessage());
				}

				string responseString = response.ToString();
				if( string.IsNullOrWhiteSpace(responseString) )
					responseString = "I did not record any errors :stuck_out_tongue:";
				await SendMessageToChannel(e.Channel, responseString);
			};
			this.Commands.Add(newCommand.Id, newCommand);

// !getException
			newCommand = new Command<TUser>("getException");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Get an exception stack for specific ID.";
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				string responseString = "I couldn't find that exception.";
				ExceptionEntry exception = null;
				if( !string.IsNullOrEmpty(e.TrimmedMessage) && int.TryParse(e.TrimmedMessage, out int id) && (exception = this.GlobalDb.Exceptions.FirstOrDefault(ex => ex.Id == id)) != null )
					responseString = exception.GetStack();

				await SendMessageToChannel(e.Channel, responseString);
			};
			this.Commands.Add(newCommand.Id, newCommand);

// !blacklist
			newCommand = new Command<TUser>("blacklist");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Add or remove an ID to or from the blacklist.";
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				guid id = 0;
				string responseString = "Invalid parameters.";
				if( e.MessageArgs == null || e.MessageArgs.Length < 2 || !guid.TryParse(e.MessageArgs[1], out id) )
				{
					if( !e.Message.MentionedUsers.Any() )
					{
						await SendMessageToChannel(e.Channel, responseString);
						return;
					}

					id = e.Message.MentionedUsers.First().Id;
				}

				ServerStats server = this.ServerDb.ServerStats.FirstOrDefault(s => s.ServerId == id || s.OwnerId == id);
				switch(e.MessageArgs[0])
				{
					case "add":
						if( this.GlobalDb.Blacklist.Any(b => b.Id == id) )
						{
							responseString = "That ID is already blacklisted.";
							break;
						}

						this.GlobalDb.Blacklist.Add(new BlacklistEntry(){Id = id});
						responseString = server == null ? "Done." : server.ServerId == id ?
								$"I'll be leaving `{server.OwnerName}`'s server `{server.ServerName}` shortly." :
								$"All of `{server.OwnerName}`'s servers are now blacklisted.";
						break;
					case "remove":
						BlacklistEntry entry = this.GlobalDb.Blacklist.FirstOrDefault(b => b.Id == id);
						if( entry == null )
						{
							responseString = "That ID was not blacklisted.";
							break;
						}

						this.GlobalDb.Blacklist.Remove(entry);
						responseString = server == null ? "Done." : server.ServerId == id ?
								$"Entry for `{server.OwnerName}`'s server `{server.ServerName}` was removed from the balcklist." :
								$"Entries for all `{server.OwnerName}`'s servers were removed from the blacklist.";
						break;
					default:
						responseString = "Invalid keyword.";
						break;
				}

				await SendMessageToChannel(e.Channel, responseString);
			};
			this.Commands.Add(newCommand.Id, newCommand);

// !subscriber
			newCommand = new Command<TUser>("subscriber");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Add or remove an ID to or from the subscribers, use with optional bonus or premium parameter.";
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e =>{
				guid id = 0;
				string responseString = "Invalid parameters.";
				if( e.MessageArgs == null || e.MessageArgs.Length < 2 ||
				    !guid.TryParse(e.MessageArgs[1], out id) )
				{
					if( !e.Message.MentionedUsers.Any() )
					{
						await SendMessageToChannel(e.Channel, responseString);
						return;
					}

					id = e.Message.MentionedUsers.First().Id;
				}

				Subscriber subscriber = this.GlobalDb.Subscribers.FirstOrDefault(s => s.UserId == id);
				switch(e.MessageArgs[0]) //Nope - mentioned users above mean that there is a parameter.
				{
					case "add":
						if( subscriber == null )
							this.GlobalDb.Subscribers.Add(subscriber = new Subscriber(){UserId = id});

						for( int i = 2; i < e.MessageArgs.Length; i++ )
						{
							subscriber.HasBonus = subscriber.HasBonus || e.MessageArgs[i] == "bonus";
							subscriber.IsPremium = subscriber.IsPremium || e.MessageArgs[i] == "premium";
						}

						responseString = "Done.";
						break;
					case "remove":
						if( subscriber == null )
						{
							responseString = "That ID was not a subscriber.";
							break;
						}

						responseString = "Done.";
						if( e.MessageArgs.Length < 3 )
						{
							this.GlobalDb.Subscribers.Remove(subscriber);
							break;
						}

						for( int i = 2; i < e.MessageArgs.Length; i++ )
						{
							subscriber.HasBonus = subscriber.HasBonus && e.MessageArgs[i] != "bonus";
							subscriber.IsPremium = subscriber.IsPremium && e.MessageArgs[i] != "premium";
						}
						break;
					default:
						responseString = "Invalid keyword.";
						break;
				}

				await SendMessageToChannel(e.Channel, responseString);
			};
			this.Commands.Add(newCommand.Id, newCommand);

// !partner
			newCommand = new Command<TUser>("partner");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Add or remove an ID to or from the partners, use with optional premium parameter.";
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e =>{
				guid id = 0;
				string responseString = "Invalid parameters.";
				if( e.MessageArgs == null || e.MessageArgs.Length < 2 ||
				    !guid.TryParse(e.MessageArgs[1], out id) )
				{
					if( !e.Message.MentionedUsers.Any() )
					{
						await SendMessageToChannel(e.Channel, responseString);
						return;
					}

					id = e.Message.MentionedUsers.First().Id;
				}

				PartneredServer partner = this.GlobalDb.PartneredServers.FirstOrDefault(s => s.ServerId == id);
				switch(e.MessageArgs[0]) //Nope - mentioned users above mean that there is a parameter.
				{
					case "add":
						if( partner == null )
							this.GlobalDb.PartneredServers.Add(partner = new PartneredServer(){ServerId = id});

						if( e.MessageArgs.Length > 2 )
							partner.IsPremium = partner.IsPremium || e.MessageArgs[2] == "premium";

						responseString = "Done.";
						break;
					case "remove":
						if( partner == null )
						{
							responseString = "That ID was not a partner.";
							break;
						}

						responseString = "Done.";
						if( e.MessageArgs.Length < 3 )
						{
							this.GlobalDb.PartneredServers.Remove(partner);
							break;
						}

						if( e.MessageArgs.Length > 2 )
							partner.IsPremium = partner.IsPremium && e.MessageArgs[2] != "premium";
						break;
					default:
						responseString = "Invalid keyword.";
						break;
				}

				await SendMessageToChannel(e.Channel, responseString);
			};
			this.Commands.Add(newCommand.Id, newCommand);

// !operations
			newCommand = new Command<TUser>("operations");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Display info about all queued or running operations on your server.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				StringBuilder response = new StringBuilder();
				bool allOperations = IsGlobalAdmin(e.Message.Author.Id);

				response.AppendLine($"Total operations in the queue: `{this.CurrentOperations.Count}`");
				if( allOperations )
					response.AppendLine($"Currently allocated data Memory: `{(GC.GetTotalMemory(false) / 1000000f):#0.00} MB`");

				response.AppendLine();
				lock( this.OperationsLock )
				{
					foreach( Operation<TUser> op in this.CurrentOperations )
					{
						if( !allOperations && op.CommandArgs.Server.Id != e.Server.Id )
							continue;

						response.AppendLine(op.ToString());
						if( allOperations )
							response.AppendLine($"Server: `{op.CommandArgs.Server.Guild.Name}`\n" +
							                    $"ServerID: `{op.CommandArgs.Server.Id}`\n" +
							                    $"Allocated DataMemory: `{op.AllocatedMemoryStarted:#0.00} MB`\n");
					}
				}

				string responseString = response.ToString();
				if( string.IsNullOrEmpty(responseString) )
					responseString = "There are no operations running.";

				await SendMessageToChannel(e.Channel, responseString);
			};
			this.Commands.Add(newCommand.Id, newCommand);

// !cancel
			newCommand = new Command<TUser>("cancel");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Cancel queued or running operation - use in the same channel, and with the name of the command as parameter. (nuke, archive, etc...)";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				string responseString = "Operation not found.";
				Operation<TUser> operation = null;

				if( !string.IsNullOrEmpty(e.TrimmedMessage) &&
				    (operation = this.CurrentOperations.FirstOrDefault(
					    op => op.CommandArgs.Channel.Id == e.Channel.Id &&
					          op.CommandArgs.Command.Id == e.TrimmedMessage)) != null )
					responseString = "Operation canceled:\n\n" + operation.ToString();

				await SendMessageToChannel(e.Channel, responseString);
			};
			this.Commands.Add(newCommand.Id, newCommand);

/*
// !command
			newCommand = new Command<TUser>("command");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "";
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				string responseString = "";
				await SendMessageToChannel(e.Channel, responseString);
			};
			this.Commands.Add(newCommand.Id, newCommand);

*/
			return Task.CompletedTask;
		}
	}
}
