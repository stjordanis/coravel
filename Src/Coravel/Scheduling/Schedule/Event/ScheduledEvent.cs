using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coravel.Invocable;
using Coravel.Scheduling.Schedule.Cron;
using Coravel.Scheduling.Schedule.Interfaces;
using Coravel.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Coravel.Scheduling.Schedule.Event
{
    public class ScheduledEvent : IScheduleInterval, IScheduledEventConfiguration
    {
        private CronExpression _expression;
        private ActionOrAsyncFunc _scheduledAction;
        private Type _invocableType = null;
        private bool _preventOverlapping = false;
        private string _eventUniqueID = null;
        private IServiceScopeFactory _scopeFactory;
        private Func<Task<bool>> _whenPredicate;
        private bool _isScheduledPerSecond = false;
        private int? _secondsInterval = null;

        public ScheduledEvent(Action scheduledAction)
        {
            this._scheduledAction = new ActionOrAsyncFunc(scheduledAction);
        }

        public ScheduledEvent(Func<Task> scheduledAsyncTask)
        {
            this._scheduledAction = new ActionOrAsyncFunc(scheduledAsyncTask);
        }

        private ScheduledEvent() { }

        public static ScheduledEvent WithInvocable<T>(IServiceScopeFactory scopeFactory) where T : IInvocable
        {
            return WithInvocableType(scopeFactory, typeof(T));
        }

        public static ScheduledEvent WithInvocableType(IServiceScopeFactory scopeFactory, Type invocableType)
        {
            var scheduledEvent = new ScheduledEvent();
            scheduledEvent._invocableType = invocableType;
            scheduledEvent._scopeFactory = scopeFactory;
            return scheduledEvent;
        }

        private static readonly int _OneMinuteAsSeconds = 60;
        public bool IsDue(DateTime utcNow)
        {
            if(this._isScheduledPerSecond)
            {
              
                if(utcNow.Second == 0)
                {
                    return _OneMinuteAsSeconds % this._secondsInterval == 0;
                }
                else {
                    return utcNow.Second % this._secondsInterval == 0;
                }
            }
            else {
                return this._expression.IsDue(utcNow);
            }
        }

        public async Task InvokeScheduledEvent()
        {
            if (await WhenPredicateFails())
            {
                return;
            }

            if (this._invocableType is null)
            {
                await this._scheduledAction.Invoke();
            }
            else
            {
                /// This allows us to scope the scheduled IInvocable object
                /// and allow DI to inject it's dependencies.
                using (var scope = this._scopeFactory.CreateScope())
                {
                    if (scope.ServiceProvider.GetRequiredService(this._invocableType) is IInvocable invocable)
                    {
                        await invocable.Invoke();
                    }
                }
            }
        }

        public bool ShouldPreventOverlapping() => this._preventOverlapping;

        public string OverlappingUniqueIdentifier() => this._eventUniqueID;

        public bool IsScheduledCronBasedTask() => !this._isScheduledPerSecond;

        public IScheduledEventConfiguration Daily()
        {
            this._expression = new CronExpression("00 00 * * *");
            return this;
        }

        public IScheduledEventConfiguration DailyAtHour(int hour)
        {
            this._expression = new CronExpression($"00 {hour} * * *");
            return this;
        }

        public IScheduledEventConfiguration DailyAt(int hour, int minute)
        {
            this._expression = new CronExpression($"{minute} {hour} * * *");
            return this;
        }

        public IScheduledEventConfiguration Hourly()
        {
            this._expression = new CronExpression($"00 * * * *");
            return this;
        }

        public IScheduledEventConfiguration HourlyAt(int minute)
        {
            this._expression = new CronExpression($"{minute} * * * *");
            return this;
        }

        public IScheduledEventConfiguration EveryMinute()
        {
            this._expression = new CronExpression($"* * * * *");
            return this;
        }

        public IScheduledEventConfiguration EveryFiveMinutes()
        {
            this._expression = new CronExpression($"*/5 * * * *");
            return this;
        }

        public IScheduledEventConfiguration EveryTenMinutes()
        {
            // todo fix "*/10" in cron part
            this._expression = new CronExpression($"*/10 * * * *");
            return this;
        }

        public IScheduledEventConfiguration EveryFifteenMinutes()
        {
            this._expression = new CronExpression($"*/15 * * * *");
            return this;
        }

        public IScheduledEventConfiguration EveryThirtyMinutes()
        {
            this._expression = new CronExpression($"*/30 * * * *");
            return this;
        }

        public IScheduledEventConfiguration Weekly()
        {
            this._expression = new CronExpression($"00 00 * * 1");
            return this;
        }

        public IScheduledEventConfiguration Cron(string cronExpression)
        {
            this._expression = new CronExpression(cronExpression);
            return this;
        }

        public IScheduledEventConfiguration Monday()
        {
            this._expression.AppendWeekDay(DayOfWeek.Monday);
            return this;
        }

        public IScheduledEventConfiguration Tuesday()
        {
            this._expression.AppendWeekDay(DayOfWeek.Tuesday);
            return this;
        }

        public IScheduledEventConfiguration Wednesday()
        {
            this._expression.AppendWeekDay(DayOfWeek.Wednesday);
            return this;
        }

        public IScheduledEventConfiguration Thursday()
        {
            this._expression.AppendWeekDay(DayOfWeek.Thursday);
            return this;
        }

        public IScheduledEventConfiguration Friday()
        {
            this._expression.AppendWeekDay(DayOfWeek.Friday);
            return this;
        }

        public IScheduledEventConfiguration Saturday()
        {
            this._expression.AppendWeekDay(DayOfWeek.Saturday);
            return this;
        }

        public IScheduledEventConfiguration Sunday()
        {
            this._expression.AppendWeekDay(DayOfWeek.Sunday);
            return this;
        }

        public IScheduledEventConfiguration Weekday()
        {
            this.Monday()
                .Tuesday()
                .Wednesday()
                .Thursday()
                .Friday();
            return this;
        }

        public IScheduledEventConfiguration Weekend()
        {
            this.Saturday()
                .Sunday();
            return this;
        }

        public IScheduledEventConfiguration PreventOverlapping(string uniqueIdentifier)
        {
            this._preventOverlapping = true;
            return this.AssignUniqueIndentifier(uniqueIdentifier);
        }

        public IScheduledEventConfiguration When(Func<Task<bool>> predicate)
        {
            this._whenPredicate = predicate;
            return this;
        }

        public IScheduledEventConfiguration AssignUniqueIndentifier(string uniqueIdentifier)
        {
            this._eventUniqueID = uniqueIdentifier;
            return this;
        }

        public Type InvocableType() => this._invocableType;

        private async Task<bool> WhenPredicateFails()
        {
            return this._whenPredicate != null && (!await _whenPredicate.Invoke());
        }

        public IScheduledEventConfiguration EverySecond()
        {
            this._secondsInterval = 1;
            this._isScheduledPerSecond = true;
            return this;
        }

        public IScheduledEventConfiguration EveryFiveSeconds()
        {
            this._secondsInterval = 5;
            this._isScheduledPerSecond = true;
            return this;
        }

        public IScheduledEventConfiguration EveryTenSeconds()
        {
            this._secondsInterval = 10;
            this._isScheduledPerSecond = true;
            return this;
        }

        public IScheduledEventConfiguration EveryFifteenSeconds()
        {
            this._secondsInterval = 15;
            this._isScheduledPerSecond = true;
            return this;
        }

        public IScheduledEventConfiguration EveryThirtySeconds()
        {
            this._secondsInterval = 30;
            this._isScheduledPerSecond = true;
            return this;
        }

        public IScheduledEventConfiguration EverySeconds(int seconds)
        {
            if(seconds < 1 || seconds > 59)
            {
                throw new ArgumentException("When calling 'EverySeconds(int seconds)', 'seconds' must be between 0 and 60");
            }
            
            this._secondsInterval = seconds;
            this._isScheduledPerSecond = true;
            return this;
        }
    }
}