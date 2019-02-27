#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LinkDev.Libraries.Common;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;

#endregion

namespace LinkDev.WfEngine.ManageSvc.Helpers
{
	internal class SlaHelper
	{
		private readonly IOrganizationService service;
		private readonly CrmLog log;

		internal SlaHelper(IOrganizationService service, CrmLog log)
		{
			this.service = service;
			this.log = log;
		}

		#region SLA

		public DateTime GetSlaTime(DateTime startTime, double notificationDuration, string durationUnit)
		{
			var notificationTime = startTime;

			//set to initial value of the day at 12:00 AM

			//DateTime dayClosingTime = notificationTime.AddSeconds(-1 * notificationTime.TimeOfDay.TotalSeconds);
			var dayClosingTime = notificationTime.Date;

			var dayStartingTime = notificationTime.Date;

			//set to initial value of the next day at 12:00 AM
			// DateTime nextDay = notificationTime.AddSeconds(-1 * notificationTime.TimeOfDay.TotalSeconds).AddDays(1);
			var nextDay = notificationTime.Date.AddDays(1);

			try
			{
				log.Log("inside GetSLATime", LogLevel.Debug);
				log.Log("Start Time" + startTime, LogLevel.Debug);
				log.Log("Duration" + notificationDuration, LogLevel.Debug);


				// DynamicEntity caseRecord = helper.RetrieveDynamicEntity(CaseId, EntityName.incident.ToString());

				var vacations = GetVacations();

				var standardWorkingHoursEntity = GetStandardWorkingHours(); //i.e: 8 hrs

				if (standardWorkingHoursEntity == null)
				{
					//standardWorkingHoursMessage = "ساعات العمل الاعتيادية غير معرفة في النظام.برجاء الاتصال بالمسئول عن النظام.";//Resources.CalculateSLAPlugin_Ar.StandardWorkingHoursValidation;


					const string standardWorkingHoursMessage =
						"The standard working hours are not defined in the system. Please contact your system administrator";

					throw new InvalidPluginExecutionException(standardWorkingHoursMessage);
				}

				var exceptionalWorkingHoursCollection = GetExceptionalWorkingHours();

				var durationInHours = notificationDuration; //sla of Task Config or Case config

				#region minutes

				#region draft

				//if (durationUnit.ToLower() == "minutes")
				//{
				//    log.LogComment("inside minutes M="+durationInHours);
				//    durationInHours = durationInHours / 60;
				//    log.LogComment("Houres Calculated "+durationInHours);
				//}

				#endregion

				if (durationUnit.ToLower() == "minutes")
				{
					durationInHours = notificationDuration/60;
					log.Log("minutes calculated " + durationInHours, LogLevel.Debug);

					#region today is vacation

					/*var isVacation = IsVacation(DateTime.Now,
                        GetDayWorkingHours(DateTime.Now, standardWorkingHoursEntity, exceptionalWorkingHoursCollection),
                        GetVactions());
                    if (isVacation)
                    {
                        var nextDate = FindNextWorkingDay(DateTime.Now);
                        var nextDayWorkingHours = GetDayWorkingHours(nextDate,
                            standardWorkingHoursEntity,
                            exceptionalWorkingHoursCollection);

                        DateTime start = (DateTime)nextDayWorkingHours["ldv_workinghoursstart"];
                        notificationTime = start.AddMinutes(notificationDuration);
                        return notificationTime;
                    }*/

					#endregion

					#region not in working hours

					//var toDayWorkingHours = GetDayWorkingHours(DateTime.Now, standardWorkingHoursEntity,
					//    exceptionalWorkingHoursCollection);
					//if (DateTime.Now > ()DateTimetoDayWorkingHours["ldv_workinghoursstart"])

					#endregion

					#region in working houres (default)

					/*notificationTime = DateTime.Now.AddMinutes(notificationDuration);
                    return notificationTime;
                    */

					#endregion
				}

				#endregion

				if (durationUnit.ToLower() == "days")
				{
					if (standardWorkingHoursEntity.Contains("ldv_workinghours"))
					{
						durationInHours = durationInHours*double.Parse(standardWorkingHoursEntity["ldv_workinghours"].ToString());
					}
				}

				var currentDayWorkingHours = GetDayWorkingHours(startTime, standardWorkingHoursEntity,
					exceptionalWorkingHoursCollection);
				if (currentDayWorkingHours.Contains("ldv_workinghoursend")) //("new_to"))
				{
					//string hoursToText = ((Picklist)workingDays.Properties["ld_to"]).name;
					var hoursToText = currentDayWorkingHours.FormattedValues["ldv_workinghoursend"];
					var hoursToParts = hoursToText.Split(' ');
					var hoursTo = double.Parse(hoursToParts[0].Split(':')[0]);
					var minutesTo = double.Parse(hoursToParts[0].Split(':')[1]);
					if (hoursToParts[1].ToLower() == "pm" && hoursTo != 12)
					{
						hoursTo += 12;
					}
					if (minutesTo == 30)
					{
						hoursTo += 0.5;
					}
					dayClosingTime = dayClosingTime.AddHours(hoursTo);
				}
				if (currentDayWorkingHours.Contains("ldv_workinghoursstart")) //("new_from"))
				{
					//string hoursFromText = ((Picklist)workingDays.Properties["ld_from"]).name;
					var hoursFromText = currentDayWorkingHours.FormattedValues["ldv_workinghoursstart"];
					var hoursFromParts = hoursFromText.Split(' ');
					var hoursFrom = double.Parse(hoursFromParts[0].Split(':')[0]);
					var minutesFrom = double.Parse(hoursFromParts[0].Split(':')[1]);
					if (hoursFromParts[1].ToLower() == "pm" && hoursFrom != 12)
					{
						hoursFrom += 12;
					}
					if (minutesFrom == 30)
					{
						hoursFrom += 0.5;
					}
					dayStartingTime = dayStartingTime.AddHours(hoursFrom);
				}

				if (IsVacation(startTime, currentDayWorkingHours, vacations) || startTime > dayClosingTime)
				{
				}
				else
				{
					if (startTime < dayStartingTime)
					{
						startTime = dayStartingTime;
					}

					notificationTime = startTime.AddHours(durationInHours);

					if (notificationTime <= dayClosingTime)
					{
						durationInHours = 0;
					}
					else
					{
						var nextDayRemainingTime = notificationTime - dayClosingTime;

						durationInHours = nextDayRemainingTime.TotalHours;
					}
				}

				while (durationInHours > 0)
				{
					var nextDayStartTime = nextDay;

					var nextDayWorkingHours = GetDayWorkingHours(nextDay, standardWorkingHoursEntity, exceptionalWorkingHoursCollection);

					while (IsVacation(nextDay, nextDayWorkingHours, vacations))
					{
						nextDay = nextDay.AddDays(1);
						nextDayWorkingHours = GetDayWorkingHours(nextDay, standardWorkingHoursEntity, exceptionalWorkingHoursCollection);
					}

					double nextDayTotalWorkingHours = 0;
					if (nextDayWorkingHours.Contains("ldv_workinghours"))
					{
						nextDayTotalWorkingHours = double.Parse(nextDayWorkingHours["ldv_workinghours"].ToString());
					}

					if (nextDayWorkingHours.Contains("ldv_workinghoursstart"))
					{
						//string hoursFromText = ((Picklist)workingDays.Properties["ld_from"]).name;
						var hoursFromText = nextDayWorkingHours.FormattedValues["ldv_workinghoursstart"];
						var hoursFromParts = hoursFromText.Split(' ');
						var hoursFrom = double.Parse(hoursFromParts[0].Split(':')[0]);
						var minutesFrom = double.Parse(hoursFromParts[0].Split(':')[1]);
						if (hoursFromParts[1].ToLower() == "pm" && hoursFrom != 12)
						{
							hoursFrom += 12;
						}
						if (minutesFrom == 30)
						{
							hoursFrom += 0.5;
						}
						nextDayStartTime = nextDay.AddHours(hoursFrom);
					}

					if (durationInHours <= nextDayTotalWorkingHours)
					{
						notificationTime = nextDayStartTime.AddHours(durationInHours);
						durationInHours = 0;
					}
					else
					{
						nextDay = nextDay.AddDays(1);
						durationInHours = durationInHours - nextDayTotalWorkingHours;
					}
				}

				return notificationTime;
			}
			catch (Exception ex)
			{
				log.Log(ex.Message, LogLevel.Error);
				//log.CatchException(ex);
				throw;
			}
		}

		internal double GetSlaDuration(DateTime dateStartingTime, DateTime dateEndingTime, DurationUnit durationUnit)
		{
			try
			{
				log.LogFunctionStart();
				var nextDay = dateStartingTime.AddDays(1);
				var durationInMinutes = 0d;
				var dateStartingHours = dateStartingTime.Hour;
				var dateStartingMinutes = dateStartingTime.Minute;

				var dateEndingHours = dateEndingTime.Hour;
				var dateEndingMinutes = dateEndingTime.Minute;

				var vacations = GetVacations(dateStartingTime);

				var standardWorkingHours = GetStandardWorkingHours();

				var exceptionalWorkingHoursCollection = GetExceptionalWorkingHours();

				var nextdayWorkingHours = 0.0;

				if (dateStartingTime < dateEndingTime)
				{
					//For First Day
					var currentDayWorkingHours = GetDayWorkingHours(dateStartingTime, standardWorkingHours,
						exceptionalWorkingHoursCollection);

					if (currentDayWorkingHours.Contains("ldv_workinghours"))
					{
						nextdayWorkingHours = double.Parse(currentDayWorkingHours["ldv_workinghours"].ToString());
					}

					if (currentDayWorkingHours.Contains("ldv_workinghoursend"))
					{
						//string hoursToText = ((Picklist)workingDays.Properties["ld_to"]).name;
						var hoursFromText = currentDayWorkingHours.FormattedValues["ldv_workinghoursstart"];
						var hoursToText = currentDayWorkingHours.FormattedValues["ldv_workinghoursend"];
						var hoursFromParts = hoursFromText.Split(' ');
						var hoursToParts = hoursToText.Split(' ');
						var hoursFrom = double.Parse(hoursFromParts[0].Split(':')[0]);
						var hoursTo = double.Parse(hoursToParts[0].Split(':')[0]);
						var minutesFrom = double.Parse(hoursFromParts[0].Split(':')[1]);
						var minutesTo = double.Parse(hoursToParts[0].Split(':')[1]);

						if (hoursFromParts[1].ToLower() == "pm" && hoursFrom != 12)
						{
							hoursFrom += 12;
						}
						if (minutesFrom == 30)
						{
							hoursFrom += 0.5;
						}

						if (hoursToParts[1].ToLower() == "pm" && hoursTo != 12)
						{
							hoursTo += 12;
						}
						if (minutesTo == 30)
						{
							hoursTo += 0.5;
						}

						if (!(IsVacation(dateStartingTime, currentDayWorkingHours, vacations)))
						{
							double sdt = (dateStartingHours*60) + dateStartingMinutes;

							if (dateStartingTime.Day != dateEndingTime.Day || dateStartingTime.Month != dateEndingTime.Month ||
							    dateStartingTime.Year != dateEndingTime.Year)
							{
								if (sdt < (hoursFrom*60))
								{
									durationInMinutes += nextdayWorkingHours*60;
								}
								else if (sdt <= (hoursTo*60))
								{
									durationInMinutes += ((hoursTo - dateStartingHours)*60) + ((0 - dateStartingMinutes));
								}
							}
						}
					}

					//For the next Days
					while (nextDay < dateEndingTime &&
					       ((nextDay.Day != dateEndingTime.Day && nextDay.Month == dateEndingTime.Month &&
					         nextDay.Year == dateEndingTime.Year) ||
					        (nextDay.Month != dateEndingTime.Month) ||
					        (nextDay.Year != dateEndingTime.Year)))
					{
						var nextDayWorkingHours = GetDayWorkingHours(nextDay, standardWorkingHours, exceptionalWorkingHoursCollection);

						while (IsVacation(nextDay, nextDayWorkingHours, vacations))
						{
							nextDay = nextDay.AddDays(1);
							nextDayWorkingHours = GetDayWorkingHours(nextDay, standardWorkingHours, exceptionalWorkingHoursCollection);
						}

						if (nextDay < dateEndingTime &&
						    ((nextDay.Day != dateEndingTime.Day && nextDay.Month == dateEndingTime.Month &&
						      nextDay.Year == dateEndingTime.Year) ||
						     (nextDay.Month != dateEndingTime.Month) ||
						     (nextDay.Year != dateEndingTime.Year)))
						{
							if (nextDayWorkingHours.Contains("ldv_workinghours"))
							{
								nextdayWorkingHours = double.Parse(nextDayWorkingHours["ldv_workinghours"].ToString());
							}

							durationInMinutes += nextdayWorkingHours*60;
							nextDay = nextDay.AddDays(1);
						}
					}

					if (dateStartingTime.Day == dateEndingTime.Day && dateStartingTime.Month == dateEndingTime.Month &&
					    dateStartingTime.Year == dateEndingTime.Year)
					{
						currentDayWorkingHours = GetDayWorkingHours(dateStartingTime, standardWorkingHours,
							exceptionalWorkingHoursCollection);

						if (!(IsVacation(dateStartingTime, currentDayWorkingHours, vacations)))
						{
							if (currentDayWorkingHours.Contains("ldv_workinghours"))
							{
								nextdayWorkingHours = double.Parse(currentDayWorkingHours["ldv_workinghours"].ToString());
							}

							if (currentDayWorkingHours.Contains("ldv_workinghoursstart") &&
							    currentDayWorkingHours.Contains("ldv_workinghoursend"))
							{
								//string hoursFromText = ((Picklist)workingDays.Properties["ld_from"]).name;
								var hoursFromText = currentDayWorkingHours.FormattedValues["ldv_workinghoursstart"];
								var hoursToText = currentDayWorkingHours.FormattedValues["ldv_workinghoursend"];
								var hoursFromParts = hoursFromText.Split(' ');
								var hoursToParts = hoursToText.Split(' ');
								var hoursFrom = double.Parse(hoursFromParts[0].Split(':')[0]);
								var hoursTo = double.Parse(hoursToParts[0].Split(':')[0]);
								var minutesFrom = double.Parse(hoursFromParts[0].Split(':')[1]);
								var minutesTo = double.Parse(hoursToParts[0].Split(':')[1]);

								if (hoursFromParts[1].ToLower() == "pm" && hoursFrom != 12)
								{
									hoursFrom += 12;
								}
								if (minutesFrom == 30)
								{
									hoursFrom += 0.5;
								}

								if (hoursToParts[1].ToLower() == "pm" && hoursTo != 12)
								{
									hoursTo += 12;
								}
								if (minutesTo == 30)
								{
									hoursTo += 0.5;
								}

								if (dateStartingTime.Day == dateEndingTime.Day &&
								    dateStartingTime.Month == dateEndingTime.Month &&
								    dateStartingTime.Year == dateEndingTime.Year)
								{
									double sdt = (dateStartingHours*60) + dateStartingMinutes;
									double edt = (dateEndingHours*60) + dateEndingMinutes;

									if (sdt > (hoursFrom*60) && edt < (hoursTo*60))
									{
										durationInMinutes += ((dateEndingHours - dateStartingHours)*60) + ((dateEndingMinutes - dateStartingMinutes));
									}
									else if (sdt >= (hoursFrom*60) && edt >= (hoursTo*60))
									{
										durationInMinutes += ((hoursTo - dateStartingHours)*60) + ((0 - dateStartingMinutes));
									}
									else if (sdt <= (hoursFrom*60) && edt < (hoursTo*60))
									{
										durationInMinutes += ((dateEndingHours - hoursFrom)*60) + ((dateEndingMinutes - 0));
									}
									else if (sdt < (hoursFrom*60) && edt >= (hoursTo*60))
									{
										durationInMinutes += nextdayWorkingHours*60;
									}
								}
								else
								{
									double edt = (dateEndingHours*60) + dateEndingMinutes;

									durationInMinutes += ((((edt >= (hoursTo*60)) ? hoursTo : dateEndingHours) - hoursFrom)*60) +
									                     ((edt >= (hoursTo*60)) ? 0 : dateEndingMinutes);
								}
							}
						}
					}
					else if (nextDay.Day == dateEndingTime.Day && nextDay.Month == dateEndingTime.Month &&
					         nextDay.Year == dateEndingTime.Year)
					{
						//Last Day
						var lastDayWorkingHours = GetDayWorkingHours(nextDay, standardWorkingHours, exceptionalWorkingHoursCollection);


						if (!IsVacation(nextDay, lastDayWorkingHours, vacations))
						{
							if (lastDayWorkingHours.Contains("ldv_workinghours"))
							{
								nextdayWorkingHours = double.Parse(lastDayWorkingHours["ldv_workinghours"].ToString());
							}

							if (lastDayWorkingHours.Contains("ldv_workinghoursstart") && lastDayWorkingHours.Contains("ldv_workinghoursend"))
							{
								//string hoursFromText = ((Picklist)workingDays.Properties["ld_from"]).name;
								var hoursFromText = lastDayWorkingHours.FormattedValues["ldv_workinghoursstart"];
								var hoursToText = lastDayWorkingHours.FormattedValues["ldv_workinghoursend"];
								var hoursFromParts = hoursFromText.Split(' ');
								var hoursToParts = hoursToText.Split(' ');
								var hoursFrom = double.Parse(hoursFromParts[0].Split(':')[0]);
								var hoursTo = double.Parse(hoursToParts[0].Split(':')[0]);
								var minutesFrom = double.Parse(hoursFromParts[0].Split(':')[1]);
								var minutesTo = double.Parse(hoursToParts[0].Split(':')[1]);

								if (hoursFromParts[1].ToLower() == "pm" && hoursFrom != 12)
								{
									hoursFrom += 12;
								}
								if (minutesFrom == 30)
								{
									hoursFrom += 0.5;
								}

								if (hoursToParts[1].ToLower() == "pm" && hoursTo != 12)
								{
									hoursTo += 12;
								}
								if (minutesTo == 30)
								{
									hoursTo += 0.5;
								}

								if (dateStartingTime.Day == dateEndingTime.Day &&
								    dateStartingTime.Month == dateEndingTime.Month &&
								    dateStartingTime.Year == dateEndingTime.Year)
								{
									double sdt = (dateStartingHours*60) + dateStartingMinutes;
									double edt = (dateEndingHours*60) + dateEndingMinutes;

									if (sdt > (hoursFrom*60) && edt < (hoursTo*60))
									{
										durationInMinutes += ((dateEndingHours - dateStartingHours)*60) + ((dateEndingMinutes - dateStartingMinutes));
									}
									else if (sdt >= (hoursFrom*60) && edt >= (hoursTo*60))
									{
										durationInMinutes += ((hoursTo - dateStartingHours)*60) + ((0 - dateStartingMinutes));
									}
									else if (sdt <= (hoursFrom*60) && edt < (hoursTo*60))
									{
										durationInMinutes += ((dateEndingHours - hoursFrom)*60) + ((dateEndingMinutes - 0));
									}
									else if (sdt < (hoursFrom*60) && edt >= (hoursTo*60))
									{
										durationInMinutes += nextdayWorkingHours*60;
									}
								}
								else
								{
									double edt = (dateEndingHours*60) + dateEndingMinutes;

									durationInMinutes += ((((edt >= (hoursTo*60)) ? hoursTo : dateEndingHours) - hoursFrom)*60) +
									                     ((edt >= (hoursTo*60)) ? 0 : dateEndingMinutes);
								}
							}
						}
					}
				}


				//To return the duration
				if (durationInMinutes == 0)
				{
					return 0;
				}

				var returnDuration = 0d;

				switch (durationUnit)
				{
					case DurationUnit.HOURS:
						returnDuration = durationInMinutes/60.0;
						break;
					case DurationUnit.MINUTES:
						returnDuration = durationInMinutes;
						break;
					case DurationUnit.SECONDS:
						returnDuration = durationInMinutes*60.0;
						break;
				}

				return returnDuration < 0 ? 0 : returnDuration;
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		private EntityCollection GetVacations(DateTime? date = null)
		{
			try
			{
				log.LogFunctionStart();
				var query = new QueryExpression
				            {
					            EntityName = "ldv_vacationdays",
					            ColumnSet = new ColumnSet(true)
				            };

				var effectiveIntervalEndCondition = new ConditionExpression("ldv_vacationenddate", ConditionOperator.GreaterEqual,
					(date ?? DateTime.Now).ToString("MM/dd/yyyy"));

				// Build the filter based on the condition.
				var filter = new FilterExpression {FilterOperator = LogicalOperator.And};
				filter.Conditions.Add(effectiveIntervalEndCondition);

				// Set the Criteria property.
				query.Criteria = filter;

				var result = service.RetrieveMultiple(query);

				return result;
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		internal Entity GetStandardWorkingHours()
		{
			try
			{
				log.LogFunctionStart();
				Entity standardWorkingHours = null;

				var query = new QueryExpression
				            {
					            EntityName = "ldv_workinghours",
					            ColumnSet = new ColumnSet(true)
				            };

				var workingHoursTypeCondition = new ConditionExpression("ldv_type", ConditionOperator.Equal, 1);

				var stateCodeCondition = new ConditionExpression("statecode", ConditionOperator.Equal, "Active");

				// Build the filter based on the condition.
				var filter = new FilterExpression {FilterOperator = LogicalOperator.And};
				filter.Conditions.AddRange(workingHoursTypeCondition, stateCodeCondition);

				// Set the Criteria property.
				query.Criteria = filter;

				var result = service.RetrieveMultiple(query);

				if (result != null && result.Entities != null && result.Entities.Count > 0)
				{
					standardWorkingHours = result.Entities[0];
				}
				return standardWorkingHours;
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		private EntityCollection GetExceptionalWorkingHours()
		{
			try
			{
				log.LogFunctionStart();
				var query = new QueryExpression
				            {
					            EntityName = "ldv_workinghours",
					            ColumnSet = new ColumnSet(true)
				            };

				var workingHoursTypeCondition = new ConditionExpression("ldv_type", ConditionOperator.Equal, 2);

				// Build the filter based on the condition.
				var filter = new FilterExpression {FilterOperator = LogicalOperator.And};
				filter.Conditions.Add(workingHoursTypeCondition);

				// Set the Criteria property.
				query.Criteria = filter;

				var result = service.RetrieveMultiple(query);

				return result;
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		private Entity GetDayWorkingHours(DateTime day, Entity standardWorkingHours, EntityCollection exceptionalWorkingHours)
		{
			try
			{
				log.LogFunctionStart();

				var currentDayWorkingHours = standardWorkingHours;

				if (exceptionalWorkingHours == null || exceptionalWorkingHours.Entities == null ||
				    exceptionalWorkingHours.Entities.Count <= 0)
				{
					return currentDayWorkingHours;
				}

				foreach (var exceptionalRecord in exceptionalWorkingHours.Entities)
				{
					var endDateNode = exceptionalRecord.Contains("ldv_dateto")
						                  ? ((DateTime) exceptionalRecord["ldv_dateto"]).ToLocalTime().ToString()
						                  : string.Empty;
					var startDateNode = exceptionalRecord.Contains("ldv_datefrom")
						                    ? ((DateTime) exceptionalRecord["ldv_datefrom"]).ToLocalTime().ToString()
						                    : string.Empty;

					if (string.IsNullOrEmpty(endDateNode) || string.IsNullOrEmpty(startDateNode))
					{
						continue;
					}

					var endDate = DateTime.Parse(endDateNode).AddHours(-1*DateTime.Parse(endDateNode).Hour);
					var startDate = DateTime.Parse(startDateNode).AddHours(-1*DateTime.Parse(startDateNode).Hour);

					if ((day >= startDate && day <= endDate)
					    || (day.Day == startDate.Day && day.Year == startDate.Year && day.Month == startDate.Month)
					    || (day.Day == endDate.Day && day.Year == endDate.Year && day.Month == endDate.Month))
					{
						currentDayWorkingHours = exceptionalRecord;
						return currentDayWorkingHours;
					}
				}

				return currentDayWorkingHours;
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		private bool IsVacation(DateTime notificationTime, Entity workingDays, EntityCollection vactionCollection)
		{
			try
			{
				log.LogFunctionStart();

				var isVacation = false;

				var currentDayAttribute = "ldv_" + notificationTime.DayOfWeek.ToString().ToLower();
				if (workingDays != null && workingDays[currentDayAttribute] != null &&
				    ((bool) workingDays[currentDayAttribute]) == false)
					//if (closeTime.DayOfWeek == DayOfWeek.Friday || closeTime.DayOfWeek == DayOfWeek.Saturday)
				{
					isVacation = true;
				}
				else
				{
					if (vactionCollection.Entities.Count <= 0)
					{
						return isVacation;
					}

					foreach (var vacation in vactionCollection.Entities)
					{
						var endDateNode = vacation.Contains("ldv_vacationenddate")
							                  ? ((DateTime) vacation["ldv_vacationenddate"]).ToLocalTime().ToString()
							                  : string.Empty;
						var startDateNode = vacation.Contains("ldv_vacationstartdate")
							                    ? ((DateTime) vacation["ldv_vacationstartdate"]).ToLocalTime().ToString()
							                    : string.Empty;

						if (string.IsNullOrEmpty(endDateNode) || string.IsNullOrEmpty(startDateNode))
						{
							continue;
						}

						var endDate = DateTime.Parse(endDateNode).AddHours(-1*DateTime.Parse(endDateNode).Hour);
						var startDate = DateTime.Parse(startDateNode).AddHours(-1*DateTime.Parse(startDateNode).Hour);

						if ((notificationTime >= startDate && notificationTime <= endDate)
						    || (notificationTime.Day == startDate.Day && notificationTime.Year == startDate.Year &&
						        notificationTime.Month == startDate.Month)
						    || (notificationTime.Day == endDate.Day && notificationTime.Year == endDate.Year &&
						        notificationTime.Month == endDate.Month))
						{
							isVacation = true;
							break;
						}
					}
				}

				return isVacation;
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		#endregion
	}

	internal enum DurationUnit
	{
		HOURS = 1,
		MINUTES = 2,
		SECONDS = 3
	}
}
