//#region Imports

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text.RegularExpressions;
//using Yagasoft.Libraries.Common;
//using Microsoft.Crm.Sdk.Messages;
//using Microsoft.Xrm.Sdk;
//using Microsoft.Xrm.Sdk.Query;

//#endregion

//namespace Yagasoft.Steps.Common.Helpers
//{
//	internal class NotificatonHelper
//	{
//		private readonly IOrganizationService service;
//		private readonly CrmLog log;
//		private readonly Helper helper;

//		internal NotificatonHelper(IOrganizationService service, CrmLog log)
//		{
//			this.service = service;
//			this.log = log;
//			helper = new Helper(service, log);
//		}

//		#region Notifications

//		internal void SendBulkNotifications(Entity regarding, Guid userId, Entity configRecord,
//			Notifications.NotificationTypeEnum notificationType, XrmServiceContext context)
//		{
//			try
//			{
//				log.LogFunctionStart();

//				log.Log("Get Notifications With Configuration ID: " + configRecord.Id + ", Notification Type: "
//				        + notificationType, LogLevel.Debug);

//				var notifications = GetNotifications(configRecord, notificationType);

//				if (notifications == null)
//				{
//					log.Log("No notifications found in this config record.");
//					return;
//				}

//				log.Log("Looping on  Notification List ...", LogLevel.Debug);
//				foreach (var notification in notifications)
//				{
//					SendNotification(regarding, userId, notification);
//				}
//			}
//			catch (Exception ex)
//			{
//				log.Log(ex);
//				throw;
//			}
//			finally
//			{
//				log.LogFunctionEnd();
//			}
//		}

//		private IEnumerable<Notifications> GetNotifications(Entity configRecord, Notifications.NotificationTypeEnum notificationType)
//		{
//			try
//			{
//				log.LogFunctionStart();

//				configRecord.Require("configRecord");

//				if (configRecord is StageConfiguration)
//				{
//					return ((StageConfiguration) configRecord).Load_Notifications(service, -1, -1, null, false, "*");
//				}
//				else if (configRecord is SLAConfiguration)
//				{
//					var filter = new FilterExpression();
//					filter.AddCondition(Notifications.Fields.NotificationType, ConditionOperator.Equal, (int) notificationType);

//					return ((SLAConfiguration) configRecord).Load_Notifications(service, -1, -1, filter, false, "*");
//				}
//				else
//				{
//					throw new Exception("Passed entity is not a supported config record.");
//				}
//			}
//			catch (Exception ex)
//			{
//				log.Log(ex);
//				throw;
//			}
//			finally
//			{
//				log.LogFunctionEnd();
//			}
//		}

//		internal void SendNotification(Entity regarding, Guid userId, Notifications notification)
//		{
//			try
//			{
//				log.LogFunctionStart();

//				if (helper.IsActivityQuick(regarding)
//				    || (!string.IsNullOrEmpty(notification.SendCondition)
//				        && !CrmHelpers.IsConditionMet(service, notification.SendCondition,
//					        regarding.ToEntityReference(), helper.IsActivityQuick(regarding))))
//				{
//					return;
//				}

//				var template = notification.Load_NotificationTemplateAsNotificationTemplate(service, false, "*");

//				if (template == null)
//				{
//					throw new InvalidPluginExecutionException("Template field has no value in notification config.");
//				}

//				var toParty = GenerateToPartyList(regarding, notification);

//				if (toParty == null || toParty.Length <= 0)
//				{
//					log.Log("Couldn't find anyone to send the notification to.", LogLevel.Warning);
//					return;
//				}

//				log.Log($"Party count: {toParty.Length}", LogLevel.Debug);

//				if (template.UseSMS.GetValueOrDefault(false))
//				{
//					SendSms(regarding, toParty, template);
//				}

//				if (!template.UseEmail.GetValueOrDefault(false))
//				{
//					log.Log("Template is not set to send email.");
//					return;
//				}

//				var firstPartyType = toParty.FirstOrDefault()?.Party?.LogicalName;
//				var isLangUnspecified = firstPartyType != "account" && firstPartyType != "contact" && firstPartyType != "systemuser";

//				var langCode = toParty.Length > 1 || isLangUnspecified
//					               ? 1033
//					               : CrmHelpers.GetPreferredLangCode(service, toParty.First().Party);
//				log.Log($"Lang code: {langCode}", LogLevel.Debug);

//				string subject;

//				if (toParty.Length > 1 || isLangUnspecified)
//				{
//					subject = template.EnglishEmailTitle + " | " + template.ArabicEmailTitle;
//				}
//				else
//				{
//					subject = langCode == 1033 ? template.EnglishEmailTitle : template.ArabicEmailTitle;
//				}

//				log.Log($"Subject: {subject}", LogLevel.Debug);

//				string body;

//				if (toParty.Length > 1 || isLangUnspecified)
//				{
//					var combine = !(string.IsNullOrEmpty(template.EnglishEmailMessage)
//					                || string.IsNullOrEmpty(template.ArabicEmailMessage));

//					body = template.EnglishEmailMessage +
//					       (combine ? "" : "\n\n-------------------------\n\n") +
//					       template.ArabicEmailMessage;
//				}
//				else
//				{
//					body = langCode == 1033 ? template.EnglishEmailMessage : template.ArabicEmailMessage;
//				}

//				log.Log("Initialize New Mail", LogLevel.Debug);
//				var newEmail = new Email
//				               {
//					               From_From = new[]
//					                           {
//						                           new ActivityParty
//						                           {
//							                           Party = new EntityReference(User.EntityLogicalName, userId)
//						                           }
//					                           },
//					               To = toParty,
//					               Subject = subject,
//					               Description = body,
//					               Regarding = regarding.ToEntityReference()
//				               };

//				log.Log("Creating New Mail", LogLevel.Debug);
//				var createdMail = service.Create(newEmail);

//				log.Log("Sending New Mail", LogLevel.Debug);
//				SendEmail(createdMail);
//			}
//			catch (Exception ex)
//			{
//				log.Log(ex);
//				throw;
//			}
//			finally
//			{
//				log.LogFunctionEnd();
//			}
//		}

//		private ActivityParty[] GenerateToPartyList(Entity regarding, Notifications notification)
//		{
//			try
//			{
//				log.LogFunctionStart();

//				switch (notification.NotificationRecipient)
//				{
//					case Notifications.NotificationRecipientEnum.ExternalCustomer:
//						return GetExternalParty(regarding, notification);

//					case Notifications.NotificationRecipientEnum.Internal:
//						return GetInternalParty(regarding, notification);

//					default:
//						throw new ArgumentOutOfRangeException();
//				}
//			}
//			catch (Exception ex)
//			{
//				log.Log(ex);
//				throw;
//			}
//			finally
//			{
//				log.LogFunctionEnd();
//			}
//		}

//		private ActivityParty[] GetExternalParty(Entity regarding, Notifications notification)
//		{
//			try
//			{
//				log.LogFunctionStart();

//				if (!notification.SendtoAccount.GetValueOrDefault(false) && !notification.SendtoContact.GetValueOrDefault(false))
//				{
//					throw new InvalidPluginExecutionException("Notification settings are invalid.");
//				}

//				var party = new List<ActivityParty>();

//				if (notification.SendtoAccount == true)
//				{
//					var account = (EntityReference) regarding.Attributes
//						                                .FirstOrDefault(attr => attr.Key == "ldv_customer"
//						                                                        || attr.Key == "customerid").Value;
//					account = account?.LogicalName == "account"
//								  ? account
//								  : (EntityReference)regarding.Attributes
//														.FirstOrDefault(attr => attr.Key == "ldv_account").Value;

//					if (account == null || account.LogicalName != "account")
//					{
//						log.Log("Trying to send to an account, but record does not contain a value for it.", LogLevel.Warning);
//					}
//					else
//					{
//						party.Add(new ActivityParty
//						          {
//							          Party = account
//						          });
//					}
//				}

//				if (notification.SendtoContact == true)
//				{
//					var contact = (EntityReference) regarding.Attributes
//						                                .FirstOrDefault(attr => attr.Key == "ldv_customer"
//						                                                        || attr.Key == "customerid").Value;
//					contact = contact?.LogicalName == "contact"
//						          ? contact
//						          : (EntityReference) regarding.Attributes
//							                              .FirstOrDefault(attr => attr.Key == "ldv_contact").Value;

//					if (contact == null || contact.LogicalName != "contact")
//					{
//						log.Log("Trying to send to an contact, but record does not contain a value for it.", LogLevel.Warning);
//					}
//					else
//					{
//						party.Add(new ActivityParty
//						          {
//							          Party = contact
//						          });
//					}
//				}

//				return party.ToArray();
//			}
//			catch (Exception ex)
//			{
//				log.Log(ex);
//				throw;
//			}
//			finally
//			{
//				log.LogFunctionEnd();
//			}
//		}

//		private ActivityParty[] GetInternalParty(Entity regarding, Notifications notification)
//		{
//			try
//			{
//				log.LogFunctionStart();

//				switch (notification.InternalNotificationType)
//				{
//					case Notifications.InternalNotificationTypeEnum.Role:
//						if (notification.Role == null)
//						{
//							throw new InvalidPluginExecutionException("Role field is not populated in notification config.");
//						}

//						return GetRoleParty(notification.Role.Value);

//					case Notifications.InternalNotificationTypeEnum.CurrentOwner:
//						if (regarding.Contains("owninguser"))
//						{
//							return new[]
//							       {
//								       new ActivityParty
//								       {
//									       Party = (EntityReference) regarding["ownerid"]
//								       }
//							       };
//						}
//						else
//						{
//							// owned by team
//							return GetMembersAsParty(CrmHelpers.GetTeamMembers(service,
//								((EntityReference) regarding["ownerid"]).Id)
//								.Select(userId => new User {Id = userId}).ToList());
//						}

//					case Notifications.InternalNotificationTypeEnum.CurrentOwnerManager:
//						if (!regarding.Contains("owninguser"))
//						{
//							throw new InvalidPluginExecutionException("Trying to notify owner's manager, but record is not owned by a user.");
//						}

//						var manager1 = CrmHelpers.GetManagerId(service,
//							((EntityReference) regarding["ownerid"]).Id);

//						if (manager1 == null)
//						{
//							throw new InvalidPluginExecutionException(
//								"Trying to notify the manager, but can't find one in the user record.");
//						}

//						return new[]
//						       {
//							       new ActivityParty
//							       {
//								       Party = new EntityReference(User.EntityLogicalName, manager1.Value)
//							       }
//						       };

//					case Notifications.InternalNotificationTypeEnum.FieldValue:
//						return GetFieldValueParty(regarding, notification);

//					case Notifications.InternalNotificationTypeEnum.CustomRecipient:
//						if (notification.CustomRecipient == null)
//						{
//							throw new InvalidPluginExecutionException("Custom recipient field is not populated in notification config.");
//						}

//						var recipients = notification
//							.Load_EmailAsCustomRecipient(service, false, Email.Fields.To).To?
//							.Select(party =>
//							        {
//								        party.Id = Guid.Empty;
//								        return party;
//							        }).ToArray();

//						if (recipients == null)
//						{
//							throw new InvalidPluginExecutionException(
//								"Trying to notify custom recipient, but the 'To' field is empty.");
//						}

//						return recipients;

//					default:
//						throw new ArgumentOutOfRangeException();
//				}
//			}
//			catch (Exception ex)
//			{
//				log.Log(ex);
//				throw;
//			}
//			finally
//			{
//				log.LogFunctionEnd();
//			}
//		}

//		private ActivityParty[] GetFieldValueParty(Entity regarding, Notifications notification)
//		{
//			try
//			{
//				log.LogFunctionStart();

//				var fieldValue = notification.FieldValue;

//				if (string.IsNullOrEmpty(fieldValue))
//				{
//					log.Log("'field value' is empty in notification record.", LogLevel.Warning);
//					return null;
//				}

//				var path = new Queue<string>();

//				foreach (Match match in Regex.Matches(fieldValue, "{.*?}"))
//				{
//					path.Enqueue(match.Value.Replace("{", "").Replace("}", ""));
//				}

//				if (path.Count <= 0)
//				{
//					log.Log("Poorly formatted 'field value' in notification record.", LogLevel.Warning);
//					return null;
//				}

//				var valueRecord = regarding;

//				while (path.Count > 1)
//				{
//					var pathNode = path.Dequeue();
//					var valueRef = valueRecord.Attributes.FirstOrDefault(pair => pair.Key == pathNode).Value as EntityReference;

//					if (valueRef == null)
//					{
//						log.Log($"'{pathNode}' field is not a lookup.", LogLevel.Warning);
//						return null;
//					}

//					valueRecord = service.Retrieve(valueRef.LogicalName, valueRef.Id, new ColumnSet(path.Peek()));
//					log.Log($"Retrieved lookup '{path.Peek()}' in '{valueRef.LogicalName}':'{valueRef.Id}'.");
//				}

//				var recipientField = path.Peek();

//				var recipient = service.Retrieve(valueRecord.LogicalName, valueRecord.Id, new ColumnSet(recipientField))
//					                .Attributes.FirstOrDefault(pair => pair.Key == recipientField).Value as EntityReference;

//				if (recipient == null)
//				{
//					log.Log($"'{recipientField}' field is null, or not a lookup.", LogLevel.Warning);
//					return null;
//				}

//				log.Log($"Retrieved lookup '{recipientField}':'{recipient.Name}' in '{valueRecord.LogicalName}':'{valueRecord.Id}'.");

//				switch (recipient.LogicalName)
//				{
//					case Team.EntityLogicalName:
//						return GetMembersAsParty(CrmHelpers.GetTeamMembers(service,
//							recipient.Id).Select(userId => new User {Id = userId}).ToList());

//					case RoleConfiguration.EntityLogicalName:
//						return GetRoleParty(recipient.Id);

//					default:
//						return new[]
//						       {
//							       new ActivityParty
//							       {
//								       Party = recipient
//							       }
//						       };
//				}
//			}
//			catch (Exception ex)
//			{
//				log.Log(ex);
//				throw;
//			}
//			finally
//			{
//				log.LogFunctionEnd();
//			}
//		}

//		private ActivityParty[] GetRoleParty(Guid roleConfigId)
//		{
//			try
//			{
//				log.LogFunctionStart();

//				var roleConfig =
//					service.Retrieve(RoleConfiguration.EntityLogicalName, roleConfigId,
//						new ColumnSet(RoleConfiguration.Fields.Type, RoleConfiguration.Fields.Team, RoleConfiguration.Fields.User,
//							RoleConfiguration.Fields.Queue)).ToEntity<RoleConfiguration>();

//				switch (roleConfig.Type)
//				{
//					case RoleConfiguration.TypeEnum.User:
//						if (roleConfig.User == null)
//						{
//							throw new InvalidPluginExecutionException("User field is not populated in role config.");
//						}

//						return new[]
//						       {
//							       new ActivityParty
//							       {
//								       Party = new EntityReference(User.EntityLogicalName, roleConfig.User.Value)
//							       }
//						       };

//					case RoleConfiguration.TypeEnum.Team:
//						if (roleConfig.Team == null)
//						{
//							throw new InvalidPluginExecutionException("Team field is not populated in role config.");
//						}

//						return GetMembersAsParty(CrmHelpers.GetTeamMembers(service,
//							roleConfig.Team.Value).Select(userId => new User {Id = userId}).ToList());

//					case RoleConfiguration.TypeEnum.Queue:
//						if (roleConfig.Queue == null)
//						{
//							throw new InvalidPluginExecutionException("Queue field is not populated in role config.");
//						}

//						return new[]
//						       {
//							       new ActivityParty
//							       {
//								       Party = new EntityReference("queue", roleConfig.Queue.Value)
//							       }
//						       };

//					default:
//						throw new ArgumentOutOfRangeException();
//				}
//			}
//			catch (Exception ex)
//			{
//				log.Log(ex);
//				throw;
//			}
//			finally
//			{
//				log.LogFunctionEnd();
//			}
//		}

//		private ActivityParty[] GetMembersAsParty(List<User> members)
//		{
//			try
//			{
//				log.LogFunctionStart();
//				return members.GroupBy(user => user.Id).Select(group => @group.First())
//					.Select(user => new ActivityParty
//					                {
//						                Party = user.ToEntityReference()
//					                }).ToArray();
//			}
//			catch (Exception ex)
//			{
//				log.Log(ex);
//				throw;
//			}
//			finally
//			{
//				log.LogFunctionEnd();
//			}
//		}

//		private void SendEmail(Guid emailId)
//		{
//			try
//			{
//				log.LogFunctionStart();

//				// Send the e-mail message.
//				service.Execute(new SendEmailRequest
//				                {
//					                EmailId = emailId,
//					                TrackingToken = "",
//					                IssueSend = true
//				                });
//			}
//			catch (Exception ex)
//			{
//				log.Log(ex);
//				throw;
//			}
//			finally
//			{
//				log.LogFunctionEnd();
//			}
//		}

//		private void SendSms(Entity regarding, ActivityParty[] parties, NotificationTemplate template)
//		{
//			try
//			{
//				log.LogFunctionStart();

//				foreach (var party in parties)
//				{
//					var partyType = party.Party.LogicalName;
//					var isLangUnspecified = partyType != "account" && partyType != "contact" && partyType != "systemuser";

//					if (isLangUnspecified)
//					{
//						log.Log($"Entity '{partyType}' has an unknown mobile phone field.", LogLevel.Warning);
//						continue;
//					}

//					var langCode = CrmHelpers.GetPreferredLangCode(service, party.Party);

//					var phoneFieldName = partyType == "account"
//						                     ? "telephone1"
//						                     : partyType == "systemuser" || partyType == "contact"
//							                       ? "mobilephone"
//							                       : "";

//					var partyPhoneNumber = (string) service.Retrieve(partyType, party.Party.Id, new ColumnSet(phoneFieldName))?
//						                                .Attributes.FirstOrDefault(pair => pair.Key == phoneFieldName).Value;

//					if (string.IsNullOrEmpty(partyPhoneNumber))
//					{
//						log.Log($"Couldn't retrieve mobile phone number from field '{phoneFieldName}'" +
//						        $" in entity '{partyType}' with ID '{party.Party.Id}'", LogLevel.Warning);
//						continue;
//					}

//					service.Create(new SMS
//					               {
//						               Subject = langCode == 1033 ? template.EnglishSMSTitle : template.ArabicSMSTitle,
//						               MobileNumber = partyPhoneNumber,
//						               Description = langCode == 1033 ? template.EnglishSMSMessage : template.ArabicSMSMessage,
//						               Regarding = regarding.ToEntityReference()
//					               });
//				}
//			}
//			catch (Exception ex)
//			{
//				log.Log(ex);
//				throw;
//			}
//			finally
//			{
//				log.LogFunctionEnd();
//			}
//		}

//		#endregion
//	}
//}
