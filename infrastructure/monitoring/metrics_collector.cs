// infrastructure/monitoring/metrics_collector.cs
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;   // System.Diagnostics.Metrics (для .NET 6+)
using Microsoft.Extensions.Logging;

namespace dnd_game.Infrastructure.Monitoring
{
    /// <summary>
    /// Интерфейс сбора метрик, используемый всеми компонентами игры.
    /// </summary>
    public interface IMetricsCollector
    {
        void IncrementCounter(string metricName, int value = 1);
        void SetGauge(string metricName, double value);
        void RecordHistogram(string metricName, double value);
        void AddTag(string tagKey, string tagValue);
        void ClearTags();
    }

    /// <summary>
    /// Конкретная реализация сборщика метрик на основе System.Diagnostics.Metrics.
    /// Экспортируется в Prometheus, Application Insights или консоль.
    /// </summary>
    public class MetricsCollector : IMetricsCollector, IDisposable
    {
        private readonly Meter _meter;
        private readonly ILogger<MetricsCollector> _logger;
        private readonly ConcurrentDictionary<string, Counter<int>> _counters = new();
        private readonly ConcurrentDictionary<string, ObservableGauge<double>> _gauges = new();
        private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new();
        private readonly ConcurrentDictionary<string, double> _gaugeValues = new(); // резервное хранилище для ObservableGauge

        // Хранилище тегов для текущего контекста (не потокобезопасно в многопоточном окружении, но для примера)
        private readonly AsyncLocal<Dictionary<string, string>?> _currentTags = new();

        public MetricsCollector(ILogger<MetricsCollector> logger)
        {
            _meter = new Meter("DnD.Game", "1.0.0");
            _logger = logger;
            InitializeDefaultMetrics();
        }

        /// <summary>
        /// Предопределённые метрики для DnD.
        /// </summary>
        private void InitializeDefaultMetrics()
        {
            // Счётчики событий
            CreateCounter("dnd.events.total", "Total domain events");
            CreateCounter("dnd.commands.total", "Total commands executed");
            CreateCounter("dnd.queries.total", "Total queries executed");

            // Счётчики персонажей
            CreateCounter("dnd.characters.created", "Characters created");
            CreateCounter("dnd.deaths.total", "Character deaths");
            CreateCounter("dnd.levels.total", "Total levels gained");

            // Боевые метрики
            CreateCounter("dnd.combat.started", "Combats started");
            CreateCounter("dnd.combat.ended", "Combats ended");
            CreateHistogram("dnd.combat.duration_seconds", "Combat duration");
            CreateCounter("dnd.attacks.total", "Total attacks");
            CreateCounter("dnd.attacks.hit", "Successful attacks");
            CreateCounter("dnd.attacks.miss", "Missed attacks");
            CreateCounter("dnd.attacks.critical_hit", "Critical hits");
            CreateCounter("dnd.damage.total", "Total damage dealt");
            CreateHistogram("dnd.damage.amount", "Damage amount distribution");

            // Урон по типам
            foreach (var dmgType in new[] { "fire", "cold", "lightning", "acid", "poison", "radiant", "necrotic", "psychic", "force", "bludgeoning", "piercing", "slashing" })
                CreateCounter($"dnd.damage.by_type.{dmgType}", $"Damage dealt ({dmgType})");

            // Хиты
            CreateCounter("dnd.healing.total", "Total healing");
            CreateHistogram("dnd.healing.amount", "Healing amount");

            // Заклинания
            CreateCounter("dnd.spells.cast", "Spells cast");
            CreateHistogram("dnd.spell.level", "Spell level distribution");
            CreateCounter("dnd.spell_slots.used", "Spell slots used");

            // Навыки
            CreateCounter("dnd.skill_checks.total", "Total skill checks");
            CreateHistogram("dnd.skill_checks.roll", "Skill check roll results");

            // Отдых
            CreateCounter("dnd.rest.started", "Rests started");
            CreateCounter("dnd.rest.ended", "Rests ended");

            // Социальное
            CreateCounter("dnd.social.interactions", "Social interactions started");

            // Ловушки
            CreateCounter("dnd.traps.triggered", "Traps triggered");

            // Gauge – активные бои (упрощённо)
            CreateGauge("dnd.combat.active", "Number of active combats");
        }

        // ---------- Реализация IMetricsCollector ----------
        public void IncrementCounter(string metricName, int value = 1)
        {
            try
            {
                if (_counters.TryGetValue(metricName, out var counter))
                {
                    counter.Add(value);
                }
                else
                {
                    // Динамическое создание счётчика
                    var newCounter = _meter.CreateCounter<int>(metricName);
                    _counters[metricName] = newCounter;
                    newCounter.Add(value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to increment counter {MetricName}", metricName);
            }
        }

        public void SetGauge(string metricName, double value)
        {
            _gaugeValues[metricName] = value;
            // Для ObservableGauge он будет читать значение из _gaugeValues.
            if (!_gauges.ContainsKey(metricName))
            {
                CreateGauge(metricName, $"Gauge: {metricName}");
            }
        }

        public void RecordHistogram(string metricName, double value)
        {
            if (_histograms.TryGetValue(metricName, out var histogram))
            {
                histogram.Record(value);
            }
            else
            {
                var newHistogram = _meter.CreateHistogram<double>(metricName);
                _histograms[metricName] = newHistogram;
                newHistogram.Record(value);
            }
        }

        public void AddTag(string tagKey, string tagValue)
        {
            // В простой реализации теги не поддерживаются на уровне метрик System.Diagnostics.Metrics напрямую,
            // но их можно добавить к конкретному инструменту при создании. Для продвинутого использования
            // следует создавать метрики с тегами через Meter.CreateCounter<int>(name, unit, description, tags).
            // Здесь мы храним теги в AsyncLocal, чтобы их можно было применить в момент вызова IncrementCounter.
            // Однако текущая реализация не использует теги при вызове Add. Это упрощение.
            var tags = _currentTags.Value ?? (_currentTags.Value = new Dictionary<string, string>());
            tags[tagKey] = tagValue;
        }

        public void ClearTags()
        {
            _currentTags.Value = null;
        }

        // ---------- Специфичные методы D&D ----------
        public void IncrementEvent(string eventType)
        {
            IncrementCounter($"dnd.events.{eventType}");
        }

        public void RecordCommandDuration(string commandType, TimeSpan duration)
        {
            RecordHistogram($"dnd.commands.duration.{commandType}", duration.TotalMilliseconds);
        }

        public void IncrementDamageByType(string damageType, int amount)
        {
            IncrementCounter($"dnd.damage.by_type.{damageType.ToLowerInvariant()}");
            RecordHistogram("dnd.damage.amount", amount);
        }

        public void RecordSpellCast(string spellName, int spellLevel)
        {
            IncrementCounter("dnd.spells.cast");
            IncrementCounter($"dnd.spells.cast.{spellName}");
            RecordHistogram("dnd.spell.level", spellLevel);
        }

        public void SetActiveCombatCount(int count)
        {
            SetGauge("dnd.combat.active", count);
        }

        // ---------- Внутренние методы создания метрик ----------
        private void CreateCounter(string name, string description)
        {
            if (!_counters.ContainsKey(name))
            {
                var counter = _meter.CreateCounter<int>(name, description: description);
                _counters[name] = counter;
            }
        }

        private void CreateGauge(string name, string description)
        {
            if (!_gauges.ContainsKey(name))
            {
                var gauge = _meter.CreateObservableGauge(name, () => Measurement(_gaugeValues.GetValueOrDefault(name, 0)), description: description);
                _gauges[name] = gauge;
            }
        }

        private void CreateHistogram(string name, string description)
        {
            if (!_histograms.ContainsKey(name))
            {
                var histogram = _meter.CreateHistogram<double>(name, description: description);
                _histograms[name] = histogram;
            }
        }

        private static Measurement<double> Measurement(double value) => new Measurement<double>(value);

        public void Dispose()
        {
            _meter?.Dispose();
        }
    }
}