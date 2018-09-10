//         Project / File: LinkDev.Plugins.Common / CustomJobHandler.cs

#region Imports

using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using static LinkDev.Libraries.Common.CrmHelpers;

#endregion

namespace LinkDev.Steps.Common
{
	public class RecurrenceNextDate : CodeActivity
	{
		[Input("Recurrence Rule")]
		[RequiredArgument]
		[ReferenceTarget(RecurrenceRule.EntityLogicalName)]
		public InArgument<EntityReference> RecurrenceRuleArg { get; set; }

		[Output("Next Target Date")]
		[RequiredArgument]
		public OutArgument<DateTime> NextTargetDateArg { get; set; }

		protected override void Execute(CodeActivityContext executionContext)
		{
			new RecurrenceNextDateLogic().Execute(this, executionContext);
		}
	}

	internal class RecurrenceNextDateLogic : StepLogic<RecurrenceNextDate>
	{
		protected override void ExecuteLogic()
		{
			var recurrenceRuleId = codeActivity.RecurrenceRuleArg.Get<EntityReference>(executionContext).Id;
			var recurrence = service.Retrieve(RecurrenceRule.EntityLogicalName, recurrenceRuleId, new ColumnSet(true))
				.ToEntity<RecurrenceRule>();

			var timeZoneShift = GetUserTimeZoneBiasMinutes(service, recurrence.Owner.Id);
			log.Log($"Recurrence owner time zone shift: {timeZoneShift}.");

			var nextRecurrence = GetNextRecurrence(recurrence, timeZoneShift);
			log.Log($"Next recurrence: '{nextRecurrence}'.");

			executionContext.SetValue(codeActivity.NextTargetDateArg, nextRecurrence ?? new DateTime(1900));
		}

		private DateTime? GetNextRecurrence(RecurrenceRule recurrence, int timeZoneShift)
		{
			try
			{
				log.LogFunctionStart();

				var exceptions = LoadExceptions(recurrence);

				DateTime? targetDate;

				log.Log($"Pattern: '{recurrence.RecurrencePattern}'");
				log.Log($"Start date: {recurrence.StartDate}");

				switch (recurrence.RecurrencePattern)
				{
					case RecurrenceRule.RecurrencePatternEnum.EveryMinute:
						targetDate = ProcessEveryMinutePattern(recurrence, exceptions, timeZoneShift);
						break;

					case RecurrenceRule.RecurrencePatternEnum.Hourly:
						targetDate = ProcessHourlyPattern(recurrence, exceptions, timeZoneShift);
						break;

					case RecurrenceRule.RecurrencePatternEnum.Daily:
						targetDate = ProcessDailyPattern(recurrence, exceptions, timeZoneShift);
						break;

					case RecurrenceRule.RecurrencePatternEnum.Weekly:
						targetDate = ProcessWeeklyPattern(recurrence, exceptions, timeZoneShift);
						break;

					case RecurrenceRule.RecurrencePatternEnum.Monthly:
						targetDate = ProcessMonthlyPattern(recurrence, exceptions, timeZoneShift);
						break;

					default:
						throw new ArgumentOutOfRangeException("RecurrencePattern",
							      $"{recurrence.RecurrencePattern} : {(int) recurrence.RecurrencePattern}",
							      "Pattern value not recognised.");
				}

				return targetDate;
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

		private DateTime? ProcessEveryMinutePattern(RecurrenceRule recurrence, RecurrenceRuleException[] exceptions,
			int timeZoneShift)
		{
			try
			{
				log.LogFunctionStart();

				var startDate = recurrence.StartDate ?? DateTime.UtcNow;
				DateTime? targetDate = startDate;
				var spanSinceStart = (int)(DateTime.UtcNow - startDate).TotalMinutes;

				var occurrenceCount = recurrence.OccurrenceCount;

				if (targetDate < DateTime.UtcNow)
				{
					if (occurrenceCount != null)
					{
						occurrenceCount -= spanSinceStart / recurrence.MinuteFrequency + 1;
						log.Log($"Remaining occurrences: {occurrenceCount}");
					}

					targetDate = targetDate?.AddMinutes(spanSinceStart - (spanSinceStart % recurrence.MinuteFrequency ?? 1));
					log.Log($"New target date after jump range: {targetDate}.", LogLevel.Debug);
				}

				var exceptionRetryCount = 500000;

				while (true)
				{
					if (occurrenceCount != null && occurrenceCount <= 0)
					{
						log.Log("Occurrences exceeded.", LogLevel.Warning);
						targetDate = null;
						break;
					}

					if (targetDate > recurrence.EndDate)
					{
						log.Log("Generated date exceeds the end date.", LogLevel.Warning);
						targetDate = null;
						break;
					}

					targetDate = targetDate.Value.AddMinutes(recurrence.MinuteFrequency ?? 1);

					// must be in the future
					if (targetDate >= DateTime.UtcNow)
					{
						// not excluded
						if (IsExcluded(targetDate.Value, exceptions, timeZoneShift))
						{
							// too many exclusion retries could lead to an infinite loop
							if (--exceptionRetryCount <= 0)
							{
								throw new InvalidPluginExecutionException("Couldn't find a target date." +
								                                          " Please relax the exclusion rules.");
							}

							continue;
						}

						break;
					}

					occurrenceCount--;
				}

				log.Log($"Target date: {targetDate}");

				return targetDate;
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

		private DateTime? ProcessHourlyPattern(RecurrenceRule recurrence, RecurrenceRuleException[] exceptions,
			int timeZoneShift)
		{
			try
			{
				log.LogFunctionStart();

				var startDate = recurrence.StartDate ?? DateTime.UtcNow;
				DateTime? targetDate = startDate;
				var spanSinceStart = (int)(DateTime.UtcNow - startDate).TotalHours;

				var occurrenceCount = recurrence.OccurrenceCount;

				if (targetDate < DateTime.UtcNow)
				{
					if (occurrenceCount != null)
					{
						occurrenceCount -= spanSinceStart / recurrence.HourlyFrequency + 1;
						log.Log($"Remaining occurrences: {occurrenceCount}");
					}

					targetDate = targetDate?.AddHours(spanSinceStart - (spanSinceStart % recurrence.HourlyFrequency ?? 1));
					log.Log($"New target date after jump range: {targetDate}.", LogLevel.Debug);
				}

				var exceptionRetryCount = 10000;

				while (true)
				{
					if (occurrenceCount != null && occurrenceCount <= 0)
					{
						log.Log("Occurrences exceeded.", LogLevel.Warning);
						targetDate = null;
						break;
					}

					if (targetDate > recurrence.EndDate)
					{
						log.Log("Generated date exceeds the end date.", LogLevel.Warning);
						targetDate = null;
						break;
					}

					targetDate = targetDate.Value.AddHours(recurrence.HourlyFrequency ?? 1);

					// must be in the future
					if (targetDate >= DateTime.UtcNow)
					{
						// not excluded
						if (IsExcluded(targetDate.Value, exceptions, timeZoneShift))
						{
							// too many exclusion retries could lead to an infinite loop
							if (--exceptionRetryCount <= 0)
							{
								throw new InvalidPluginExecutionException("Couldn't find a target date." +
																		  " Please relax the exclusion rules.");
							}

							continue;
						}

						break;
					}

					occurrenceCount--;
				}

				log.Log($"Target date: {targetDate}");

				return targetDate;
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

		private DateTime? ProcessDailyPattern(RecurrenceRule recurrence, RecurrenceRuleException[] exceptions,
			int timeZoneShift)
		{
			try
			{
				log.LogFunctionStart();

				var startDate = recurrence.StartDate ?? DateTime.UtcNow;
				DateTime? targetDate = startDate;
				var spanSinceStart = (int)(DateTime.UtcNow - startDate).TotalDays;

				var occurrenceCount = recurrence.OccurrenceCount;

				if (targetDate < DateTime.UtcNow)
				{
					if (occurrenceCount != null)
					{
						occurrenceCount -= spanSinceStart / recurrence.DailyFrequency + 1;
						log.Log($"Remaining occurrences: {occurrenceCount}");
					}

					targetDate = targetDate?.AddDays(spanSinceStart - (spanSinceStart % recurrence.DailyFrequency ?? 1));
					log.Log($"New target date after jump range: {targetDate}.", LogLevel.Debug);
				}

				var exceptionRetryCount = 600;

				while (true)
				{
					if (occurrenceCount != null && occurrenceCount <= 0)
					{
						log.Log("Occurrences exceeded.", LogLevel.Warning);
						targetDate = null;
						break;
					}

					if (targetDate > recurrence.EndDate)
					{
						log.Log("Generated date exceeds the end date.", LogLevel.Warning);
						targetDate = null;
						break;
					}

					targetDate = targetDate.Value.AddDays(recurrence.DailyFrequency ?? 1);

					// must be in the future
					if (targetDate >= DateTime.UtcNow)
					{
						// not excluded
						if (IsExcluded(targetDate.Value, exceptions, timeZoneShift))
						{
							// too many exclusion retries could lead to an infinite loop
							if (--exceptionRetryCount <= 0)
							{
								throw new InvalidPluginExecutionException("Couldn't find a target date." +
								                                          " Please relax the exclusion rules.");
							}

							continue;
						}

						break;
					}

					occurrenceCount--;
				}

				log.Log($"Target date: {targetDate}");

				return targetDate;
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

		private DateTime? ProcessWeeklyPattern(RecurrenceRule recurrence, RecurrenceRuleException[] exceptions,
			int timeZoneShift)
		{
			try
			{
				log.LogFunctionStart();

				var startDate = recurrence.StartDate ?? DateTime.UtcNow;
				var userLocalNow = DateTime.UtcNow.AddMinutes(timeZoneShift);
				var nextWeeklyBase = startDate.AddMinutes(timeZoneShift);
				DateTime? targetDate = nextWeeklyBase;
				var spanSinceStart = (int)((userLocalNow - nextWeeklyBase).TotalDays / 7);

				var occurrenceCount = recurrence.OccurrenceCount;

				if (targetDate < userLocalNow)
				{
					if (occurrenceCount != null)
					{
						occurrenceCount -= spanSinceStart / recurrence.WeeklyFrequency + 1;
						log.Log($"Remaining occurrences: {occurrenceCount}");
					}

					targetDate = targetDate?.AddDays((spanSinceStart - (spanSinceStart % recurrence.WeeklyFrequency ?? 1)) * 7);
					log.Log($"New target date after jump range: {targetDate}.", LogLevel.Debug);
				}

				var weeklyDays = recurrence.WeekDays.Split(',');
				
				var isFound = false;
				var exceptionRetryCount = 5000;

				while (!isFound)
				{
					if (occurrenceCount != null && occurrenceCount <= 0)
					{
						log.Log("Occurrences exceeded.", LogLevel.Warning);
						targetDate = null;
						break;
					}

					if (targetDate > recurrence.EndDate?.AddMinutes(timeZoneShift))
					{
						log.Log("Generated date exceeds the end date.", LogLevel.Warning);
						targetDate = null;
						break;
					}

					for (var i = 0; i <= 7; i++)
					{
						var nextDay = nextWeeklyBase.AddDays(i);

						if (weeklyDays.Contains(nextDay.ToString("dddd").ToLower()))
						{
							// must be in the future
							if (nextDay >= userLocalNow)
							{
								// not excluded
								if (IsExcluded(nextDay, exceptions))
								{
									// too many exclusion retries could lead to an infinite loop
									if (--exceptionRetryCount <= 0)
									{
										throw new InvalidPluginExecutionException("Couldn't find a target date." +
																				  " Please relax the exclusion rules.");
									}

									continue;
								}
							}
							else
							{
								occurrenceCount--;
								continue;
							}

							targetDate = nextDay;
							isFound = true;
							log.Log("Found!", LogLevel.Debug);

							break;
						}
					}

					nextWeeklyBase = nextWeeklyBase.AddDays(7*(recurrence.WeeklyFrequency ?? 1));
				}

				log.Log($"Target date: {targetDate}");

				return targetDate?.AddMinutes(-timeZoneShift);
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

		private DateTime? ProcessMonthlyPattern(RecurrenceRule recurrence, RecurrenceRuleException[] exceptions,
			int timeZoneShift)
		{
			try
			{
				log.LogFunctionStart();

				var startDate = recurrence.StartDate ?? DateTime.UtcNow;
				var userLocalNow = DateTime.UtcNow.AddMinutes(timeZoneShift);
				var nextMonthlyBase = startDate.AddMinutes(timeZoneShift);
				DateTime? targetDate = nextMonthlyBase;

				var occurrenceCount = recurrence.OccurrenceCount;

				if (occurrenceCount == null)
				{
					if (targetDate < userLocalNow)
					{
						targetDate = targetDate?.AddDays((int)(userLocalNow - nextMonthlyBase).TotalDays);
						log.Log($"New target date after jump range: {targetDate}.", LogLevel.Debug);
					}
				}
				else
				{
					log.Log($"Remaining occurrences: {occurrenceCount}");
				}

				var months = recurrence.Months.Split(',');
				var daysOfTheMonth = recurrence.DaysOfTheMonth.Split(',');
				var occurrences = recurrence.DayOccurrences?.Split(',')
					.Select(e => (RecurrenceRule.MonthlyDayOccurrenceEnum)
								RecurrenceRule.Enums.GetValue(RecurrenceRule.Fields.MonthlyDayOccurrence,
									e.ToTitleCase())).ToArray();
				var daysOfWeek = recurrence.WeekDays?.Split(',')
					.Select(e => (RecurrenceRule.WeekDayEnum)
								RecurrenceRule.Enums.GetValue(RecurrenceRule.Fields.WeekDay, e.ToTitleCase()))
					.ToArray();

				var isFound = false;
				var exceptionRetryCount = 5000;

				while (true)
				{
					if (occurrenceCount != null && occurrenceCount <= 0)
					{
						log.Log("Occurrences exceeded.", LogLevel.Warning);
						targetDate = null;
						break;
					}

					if (targetDate > recurrence.EndDate?.AddMinutes(timeZoneShift))
					{
						log.Log("Generated date exceeds the end date.", LogLevel.Warning);
						targetDate = null;
						break;
					}

					// must be in the future, and month selected
					if (months.Contains(nextMonthlyBase.ToString("MMMM").ToLower()) && nextMonthlyBase >= userLocalNow)
					{
						switch (recurrence.MonthlyPattern)
						{
							case RecurrenceRule.MonthlyPatternEnum.SpecificDays:
								if (daysOfTheMonth.Contains(nextMonthlyBase.Day.ToString()))
								{
									isFound = true;
								}

								break;

							case RecurrenceRule.MonthlyPatternEnum.DayOccurrence:
								if (daysOfWeek == null && occurrences != null)
								{
									if (occurrences.Contains(RecurrenceRule.MonthlyDayOccurrenceEnum.Last))
									{
										isFound = nextMonthlyBase.Day
											== DateTime.DaysInMonth(nextMonthlyBase.Year, nextMonthlyBase.Month);
									}
								}

								for (var j = 0; j < daysOfWeek?.Length && !isFound; j++)
								{
									for (var k = 0; k < occurrences?.Length && !isFound; k++)
									{
										var isLastOccurrence = occurrences[j] == RecurrenceRule.MonthlyDayOccurrenceEnum.Last;
										var dateTimeDayOfWeek = GetDayOfWeek(daysOfWeek[j]);
										var occurrenceDate = DateTimeHelpers.GetDayOccurrenceOfMonth(nextMonthlyBase, dateTimeDayOfWeek,
											(int) occurrences[j], isLastOccurrence);

										if (occurrenceDate != null && occurrenceDate == nextMonthlyBase)
										{
											isFound = true;
										}
									}
								}

								break;

							default:
								throw new ArgumentOutOfRangeException("MonthlyPattern",
									      $"{recurrence.MonthlyPattern} : {(int) recurrence.MonthlyPattern}",
									      "Pattern value not recognised.");
						}
					}

					if (isFound)
					{
						// must not be excluded
						if (!IsExcluded(nextMonthlyBase, exceptions))
						{
							targetDate = nextMonthlyBase;
							log.Log("Found!", LogLevel.Debug);
							break;
						}

						// too many exclusion retries could lead to an infinite loop
						if (--exceptionRetryCount <= 0)
						{
							throw new InvalidPluginExecutionException("Couldn't find a target date." +
																	  " Please relax the exclusion rules.");
						}
					}

					occurrenceCount--;
					isFound = false;
					nextMonthlyBase = nextMonthlyBase.AddDays(1);
				}

				log.Log($"Target date: {targetDate}");

				return targetDate?.AddMinutes(-timeZoneShift);
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

		private bool IsExcluded(DateTime utcDate, RecurrenceRuleException[] exceptions, int timeZoneShift = 0)
		{
			var userLocalDate = utcDate.AddMinutes(timeZoneShift);

			foreach (var exception in exceptions)
			{
				var isMinuteExcluded = exception.Minutes?.Split(',').Contains(userLocalDate.Minute.ToString()) != false;
				var isHourExcluded = exception.Hours?.Split(',').Contains(userLocalDate.Hour.ToString()) != false;
				var isDayExcluded = exception.DaysOfTheMonth?.Split(',').Contains(userLocalDate.Day.ToString()) != false;
				var isMonthExcluded = exception.Months?.Split(',').Contains(userLocalDate.ToString("MMMM").ToLower()) != false;
				var isYearExcluded = exception.Years?.Split(',').Contains(userLocalDate.Year.ToString()) != false;
				var isInRange = (exception.StartDate != null || exception.EndDate != null)
								&& (exception.StartDate == null || userLocalDate >= exception.StartDate?.AddMinutes(timeZoneShift))
								&& (exception.EndDate == null || userLocalDate <= exception.EndDate?.AddMinutes(timeZoneShift));
				var isWeekDayExcluded = true;

				if (exception.DayOccurrences == null)
				{
					isWeekDayExcluded = exception.WeekDays?.Split(',').Contains(userLocalDate.ToString("dddd").ToLower())
						 != false;
				}
				else
				{
					var occurrences = exception.DayOccurrences?.Split(',')
						.Select(e => (RecurrenceRuleException.MonthlyDayOccurrenceEnum)
									RecurrenceRuleException.Enums.GetValue(RecurrenceRuleException.Fields.MonthlyDayOccurrence,
										e.ToTitleCase())).ToArray();

					if (exception.WeekDays == null)
					{
						if (occurrences.Contains(RecurrenceRuleException.MonthlyDayOccurrenceEnum.Last))
						{
							isWeekDayExcluded =
								userLocalDate.Day == DateTime.DaysInMonth(userLocalDate.Year, userLocalDate.Month);
						}
					}
					else
					{
						log.LogLine();
						var daysOfWeek = exception.WeekDays?.Split(',')
							.Select(e => (RecurrenceRule.WeekDayEnum)
										RecurrenceRule.Enums.GetValue(RecurrenceRule.Fields.WeekDay, e.ToTitleCase()))
							.ToArray();

						if (daysOfWeek.Any())
						{
							var isFound = false;

							for (var index = 0; index < daysOfWeek.Length && !isFound; index++)
							{
								var dayOfWeek = daysOfWeek[index];

								for (var i = 0; i < occurrences.Length && !isFound; i++)
								{
									var occurrence = occurrences[i];

									var dateTimeDayOfWeek = GetDayOfWeek(dayOfWeek);
									var isLastOccurrence = occurrence == RecurrenceRuleException.MonthlyDayOccurrenceEnum.Last;
									var occurrenceDate = DateTimeHelpers.GetDayOccurrenceOfMonth(userLocalDate, dateTimeDayOfWeek,
										(int) occurrence, isLastOccurrence);

									isFound = isWeekDayExcluded = userLocalDate.Day == occurrenceDate?.Day;
								}
							}
						}
					}
				}

				if (isInRange
					|| (isMinuteExcluded && isHourExcluded && isDayExcluded
						&& isWeekDayExcluded && isMonthExcluded && isYearExcluded))
				{
					return true;
				}

				log.Log($"ID: {exception.Id}", LogLevel.Debug);

				if (exception.Minutes != null)
				{
					log.Log($"Minutes: {exception.Minutes}", LogLevel.Debug);
				}

				log.Log($"Minute Excluded: {isMinuteExcluded}", LogLevel.Debug);

				if (exception.Hours != null)
				{
					log.Log($"Hours: {exception.Hours}", LogLevel.Debug);
					log.Log($"Hour Excluded: {isHourExcluded}", LogLevel.Debug);
				}

				if (exception.DaysOfTheMonth != null)
				{
					log.Log($"Days of the month: {exception.DaysOfTheMonth}", LogLevel.Debug);
				}

				log.Log($"Day Excluded: {isDayExcluded}", LogLevel.Debug);

				if (exception.WeekDays != null)
				{
					log.Log($"Week days: {exception.WeekDays}", LogLevel.Debug);
				}

				log.Log($"Week day Excluded: {isWeekDayExcluded}", LogLevel.Debug);

				if (exception.Months != null)
				{
					log.Log($"Months: {exception.Months}", LogLevel.Debug);
				}

				log.Log($"Month Excluded: {isMonthExcluded}", LogLevel.Debug);

				if (exception.Years != null)
				{
					log.Log($"Years: {exception.Years}", LogLevel.Debug);
				}

				log.Log($"Year Excluded: {isYearExcluded}", LogLevel.Debug);

				log.Log($"Target Date: {utcDate}", LogLevel.Debug);
				log.Log($"Start date: {exception.StartDate} UTC", LogLevel.Debug);
				log.Log($"End date: {exception.EndDate} UTC", LogLevel.Debug);
				log.Log($"Is in exclusion range: {isInRange}", LogLevel.Debug);
			}

			return false;
		}

		private RecurrenceRuleException[] LoadExceptions(RecurrenceRule recurrence)
		{
			try
			{
				log.LogFunctionStart();

				log.Log("Loading exceptions ...", LogLevel.Debug);

				recurrence.LoadRelation(RecurrenceRule.RelationNames.Exceptions, service, "*");
				recurrence.LoadRelation(RecurrenceRule.RelationNames.ExceptionGroupings, service, "*");

				var exceptions = new RecurrenceRuleException[0];

				if (recurrence.Exceptions != null)
				{
					exceptions = exceptions.Union(recurrence.Exceptions).ToArray();
				}

				if (recurrence.ExceptionGroupings != null)
				{
					log.Log("Loading exceptions in groupings ...", LogLevel.Debug);

					foreach (var grouping in recurrence.ExceptionGroupings)
					{
						grouping.LoadRelation(RecurrenceRuleExceptionGrouping.RelationNames.Exceptions, service, "*");

						if (grouping.Exceptions != null)
						{
							exceptions = exceptions.Union(grouping.Exceptions).ToArray();
						}
					}
				}

				log.Log($"Exceptions count: {exceptions.Length}");

				return exceptions;
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

		private static DayOfWeek GetDayOfWeek(RecurrenceRule.WeekDayEnum weekDay)
		{
			var dateTimeDayOfWeek = DayOfWeek.Sunday;

			switch (weekDay)
			{
				case RecurrenceRule.WeekDayEnum.Sunday:
					dateTimeDayOfWeek = DayOfWeek.Sunday;
					break;

				case RecurrenceRule.WeekDayEnum.Monday:
					dateTimeDayOfWeek = DayOfWeek.Monday;
					break;

				case RecurrenceRule.WeekDayEnum.Tuesday:
					dateTimeDayOfWeek = DayOfWeek.Tuesday;
					break;

				case RecurrenceRule.WeekDayEnum.Wednesday:
					dateTimeDayOfWeek = DayOfWeek.Wednesday;
					break;

				case RecurrenceRule.WeekDayEnum.Thursday:
					dateTimeDayOfWeek = DayOfWeek.Thursday;
					break;

				case RecurrenceRule.WeekDayEnum.Friday:
					dateTimeDayOfWeek = DayOfWeek.Friday;
					break;

				case RecurrenceRule.WeekDayEnum.Saturday:
					dateTimeDayOfWeek = DayOfWeek.Saturday;
					break;
			}

			return dateTimeDayOfWeek;
		}
	}
}
