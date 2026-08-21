1.	RestHandler.cs: Используется CancellationToken.None вместо переданного cancellationToken во всех методах.
2.	CharacterProjection.cs: В Apply(CharacterCreated e) не инициализируются MaxSpellSlots, HitDiceRemaining, MaxHitDice – позже обращения могут дать NullReferenceException.
3.	CharacterProjection.cs: Apply(VulnerabilityRemoved), Apply(ImmunityRemoved), Apply(ResistanceRemoved) не вызывают InvalidateCache.
4.	CharacterProjection.cs: RebuildAsync применяет только CharacterCreated, а не все события – проекция восстановится некорректно.
5.	CharacterDto.cs: Дублирование/путаница: HitDice и HitDiceRemaining/MaxHitDice; SpellSlots и MaxSpellSlots/UsedSpellSlots.
6.	CharacterDto.cs: Многие свойства объявлены как = null!, что опасно при отсутствии инициализации.
7.	CombatProjection.cs + combat_dto.cs: CombatParticipantDto.Conditions имеет значение null! по умолчанию, но в CombatStarted и ParticipantAddedToCombat не инициализируется.
8.	CombatProjection.cs: В Apply(ConditionAppliedToCombatant) и Apply(ConditionRemovedFromCombatant) вызываются p.Conditions.Append(...) / p.Conditions.Where(...) без проверки на null → возможен NullReferenceException.
9.	CombatProjection.cs: Скорость MovementRemaining = 30 захардкожена вместо использования реальной скорости персонажа.
10.	CampaignProjection.cs: При изменении репутации фракции инвалидируется только campaign:factions:all, но не campaign:faction:{factionId} – кеш отдельной фракции может устареть.
11.	MovementHandler.cs: MoveCharacter проверяет только целевую клетку и Чебышёвское расстояние, но не реальную стоимость пути по клеткам с разным terrain.
12.	MovementHandler.cs: MoveCharacterToPosition вообще не проверяет скорость/дистанцию.
13.	MovementHandler.cs: В MoveCharacter сравнение remainingSpeed < costPerCell не учитывает фактическое расстояние. Присутствуют нечитаемые артефакты кодировки в комментариях (mojibake).
14.	CombatHandler.cs: В Handle(PerformAction) для действия move предполагается, что ActionData — это int; если это не так, получится 0 и команда будет некорректной. Жёстко завязан на типы команд, нет проверки на null для ActionData.
15.	DialogService.cs: StartDialogue отправляет StartDialogueCommand, но в других местах (например, TriggerHandler) используется StartDialogCommand – возможна несогласованность имён команд.
16.	DialogService.cs: SelectOption бросает исключение, если есть проверка навыка, вместо того чтобы перенаправить в ResolveSkillCheck.
17.	DialogService.cs: EvaluateCondition для QuestCompleted, ReputationAbove, FlagSet всегда возвращает true (заглушки) – неверное поведение.
18.	CraftingService.cs: new RemoveInventoryItem(characterId, comp.ComponentId) не передаёт количество, хотя команда RemoveInventoryItem может принимать Quantity.
19.	TradeService.cs: Аналогично в TradeService: удаление предметов через цикл по одному без передачи Quantity.
20.	TradeService.cs: BuyItemFromNpc использует _ = await characterProjection.GetById(...) вместо сохранения результата – работает, но неаккуратно.
21.	TradeService.cs: Комментарий к GetCharacterGold называет метод заглушкой, хотя поле Gold уже есть в CharacterDto.
22.	TravelService.cs: SpecialMovement для default-ветки вызывает MoveCharacterToPosition(characterId, distanceFeet, 0, movementType), что передаёт расстояние как координату X и 0 как Y – семантически неверно.
23.	TravelService.cs: Часть команд (StartJourneyCommand, EndJourneyCommand и др.) может не существовать в Domain – проверить согласованность.
24.	Security namespace: Пространства имён не согласованы: dnd_game.application.security (интерфейс ICurrentUserService) и dnd_game.Application.Security (реализация CurrentUserService, PermissionChecker, PolicyEnforcer) – лучше унифицировать.
25.	Общее: Повсеместно смешаны dnd_game.application.* и dnd_game.Application.* – привести к одному стилю.
26.	Общее: В нескольких файлах (LoggingHandler, MetricHandler, NotificationHandler) названия свойств событий могут не совпадать с реальными классами событий (например, ConditionApplied.Condition vs ConditionApplied.ConditionType) – проверить.
27.	character_aggregate.cs: Не обрабатываются многие события в ApplyEvent: RestStarted, RestInterrupted, RestCompleted, ProficiencyBonusUpdated, SpellPrepared, SpellUnprepared, ClassFeatureUsed, ClassFeatureRecharged, ItemAttuned, ItemUnattuned, DeathSavingThrowsReset, CharacterDashed, CharacterDisengaged, CharacterHid, CharacterClimbed, CharacterSwam, CharacterFlew, CharacterBurrowed, CharacterJumped, CharacterSpeedChanged, CharacterSpeedReset, DifficultTerrainApplied, DifficultTerrainRemoved, MovementImpaired, MovementRestored, AthleticsCheckForMovementMade, AcrobaticsCheckForMovementMade, FallDamageTaken – состояние не обновляется, эффекты команд теряются.
28.	character_aggregate.cs: HealHitPoints не лечит персонажа, если HitPoints == 0 && !IsStable – умирающий персонаж не может быть исцелён, хотя по правилам может.
29.	character_aggregate.cs: CastSpell не проверяет доступные ячейки заклинаний (только наличие уровня в MaxSpellSlots), в отличие от UseSpellSlot – можно колдовать без ячеек.
30.	character_aggregate.cs: SpendHitDie вычисляет healed = roll + constitutionModifier, но событие HitDieSpent обрабатывается без увеличения HP – короткий отдых не восстанавливает хиты.
31.	character_aggregate.cs: TakeShortRest(int v) игнорирует переданный список костей и просто отправляет RestCompleted("Short", 0) – некорректная реализация.
32.	character_aggregate.cs: ApplyEvent(SpellSlotsRestored) сбрасывает все использованные ячейки, хотя событие содержит SlotLevel и RestoredCount – при частичном восстановлении сбрасываются все.
33.	character_aggregate.cs: MakeSavingThrow не порождает никакого события – попытка спасброска не сохраняется в истории.
34.	character_aggregate.cs: TakeFallDamage сначала применяет FallDamageTaken, затем TakeDamage, но событие FallDamageTaken не обрабатывается (только для записи) – возможно, задумано только событие, но сейчас двойной эффект (если бы обрабатывалось).
35.	character_aggregate.cs: SetTemporaryHitPoints использует Math.Max(TemporaryHitPoints, e.Amount) – нельзя установить меньшее значение временных хитов (по правилам можно заменить).
36.	combat_aggregate.cs: Сигнатура ApplyEvent(Events.IDomainEvent @event) с префиксом Events. – вероятная ошибка компиляции (должно быть IDomainEvent).
37.	combat_aggregate.cs: CombatRoundStarted сбрасывает HasReaction = true для всех участников в начале раунда – реакция должна восстанавливаться в начале хода персонажа, а не раунда (сейчас реакция доступна дважды за раунд).
38.	combat_aggregate.cs: StartRound проверяет if (Participants.Any(p => p.Initiative == 0 && p.CharacterId != Guid.Empty)) – нельзя отличить «не бросил инициативу» от легитимного результата 0 или отрицательного (при отрицательном модификаторе Ловкости).
39.	combat_aggregate.cs: При ParticipantRemovedFromCombat не корректируется CurrentTurnIndex – после удаления текущего участника NextTurn может начать не с того места.
40.	combat_aggregate.cs: EndRound пустой, хотя может требоваться отправка события/логики завершения раунда.
41.	combat_aggregate.cs: ReadyAction не сохраняет готовое действие и не тратит реакцию/действие – готовое действие фактически не работает.
42.	campaign_aggregate.cs: В ApplyEvent(QuestCreated) игнорируются ParticipantIds – участники квеста не сохраняются в состоянии агрегата.
43.	campaign_aggregate.cs: AcceptQuest не проверяет статус квеста (можно принять уже активный/завершённый) – нет проверки, что квест доступен.
44.	combat_commands.cs, character_commands.cs: ChangeFactionReputation в campaign_commands.cs не имеет суффикса Command, в отличие от остальных – несогласованность именования.
45.	character_commands.cs: TakeShortRest содержит List<(int HitDieType, int Roll, int ConstitutionModifier)>? HitDiceSpent, но обработчик в CharacterHandler передаёт только количество, а не сами броски – информация о бросках теряется.
46.	MovementRules.cs + IGridProvider.cs: В MovementRules используется grid.GetCell(pos.X, pos.Y).Terrain, но GridCell из IGridProvider.cs не содержит свойства Terrain – ошибка компиляции.
47.	MovementRules.cs + IGridProvider.cs: Дублируются using с разным регистром пространства имён (dnd_game.domain.value_objects vs dnd_game.Domain.ValueObjects) – риск конфликтов.
48.	MovementRules.cs + IGridProvider.cs: IGridProvider определён в dnd_game.Domain.Interfaces, но используется using dnd_game.Infrastructure.World в MovementRules – несогласованность.
49.	dice.cs: Парсинг нотации ro (reroll on or less) ошибочен: int.Parse(match.Groups["reroll"].Value) где группа reroll захватывает "ro2", а не "2" – Dice.Parse("2d6ro2+1") выбросит FormatException.
50.	dice.cs: Математика в Average для RerollOnOrLess неверна – переброс низких значений увеличивает среднее, а код оставляет его неизменным.
51.	quest_saga.cs: OnQuestCompleted выдаёт награды всем персонажам из GetAll(), а не только участникам квеста – награды получат посторонние персонажи.
52.	quest_saga.cs: PlayerCharacterIds в CombatSaga никогда не заполняется, поэтому IsCombatOver() всегда возвращает true после удаления любого участника – бой завершается преждевременно.
53.	combat_saga.cs: При старте боя для каждого участника отправляется RollInitiative(..., 0, 0), т.е. инициатива всегда 0 – не выполняется реальный бросок инициативы.
54.	combat_saga.cs: CombatSaga в конструкторе и OnCombatStarted пересоздаёт состояние, что может затирать загруженное из репозитория.
55.	levelup_saga.cs: hitDieType фиксирован = 8 для всех классов – для воинов (d10), магов (d6) и т.д. кость хитов неверна.
56.	levelup_saga.cs: UpdateSpellSlots использует MagicRules.FullCasterSpellSlots для всех персонажей – паладин/следопыт должны использовать HalfCasterSpellSlots, а воин/плут – не получать ячейки вовсе.
57.	trade_saga.cs: Компенсация возвращает предметы и золото обеим сторонам полностью, независимо от того, какие шаги уже выполнились – при частичном выполнении возможен повторный возврат (нет идемпотентности).
58.	trade_saga.cs: В CompensateTrade команды отправляются без CancellationToken.
59.	Общее: Многие события не реализуют ICharacterEvent / ICombatEvent / ICampaignEvent, хотя соответствующие базовые интерфейсы определены – не работают базовые проверки в CharacterEventhandlerBase и других местах.
60.	Общее: Пространство имён dnd_game.domain.value_objects (нижний регистр) используется в нескольких файлах, тогда как в остальном проекте принят dnd_game.Domain.ValueObjects (верхний регистр) – унифицировать.
61.	Общее: Ряд команд/событий существуют, но не используются обработчиками (например, DeleteQuestCommand, HelpAction, HideAction, SearchAction, UseObjectAction) – либо добавить обработчики, либо удалить.
62.	Общее: В MagicRules методы RequiresConcentration, IsCantrip всегда возвращают true, а CanCastSpell – true без проверок – заглушки приводят к некорректному поведению.
63.	Общее: RestRules.RechargesOnShortRest и RechargesOnLongRest бросают исключение при пустом featureId, но затем возвращают фиксированные значения – непоследовательное поведение.
64.	postgres_event_store.cs: Критично: Load<T> при использовании снапшота применяет события через ApplyChange, а не через LoadFromHistory/ApplyEvent – это добавляет загруженные события в список несохранённых и увеличивает версию, что приводит к дублированию при следующем сохранении.
65.	postgres_event_store.cs: Игнорируется CancellationToken в явных реализациях интерфейса (Save, Load) – токен теряется.
66.	postgres_event_store.cs: SaveWithMetadata не принимает CancellationToken и не пробрасывает его в SaveInternal/EnforceConsistencyAsync.
67.	postgres_event_store.cs: ReadStoredEvent не обрабатывает type == null – будет NullReferenceException при неизвестном типе события.
68.	postgres_event_store.cs: GetAllEvents возвращает IEnumerable<object>, а проекции ожидают IDomainEvent; в CharacterProjection.RebuildAsync слепое приведение к CharacterCreated вызовет ошибку для любого другого события.
69.	postgres_event_store.cs: InitializeDatabase закомментирован – схема может не создаться.
70.	postgres_event_store.cs: Пространство имён файла dnd_game.infrastructure.event_store (нижний регистр) расходится с dnd_game.Infrastructure.EventStore (верхний) в других местах.
71.	snapshot_store.cs: Сериализация агрегата JsonSerializer.Serialize(aggregate, aggregate.GetType()) не сериализует приватные сеттеры и поля без настройки – снапшоты будут пустыми или некорректными.
72.	snapshot_store.cs: Политика TimeInterval/Manual не реализована – ShouldCreateSnapshotAsync всегда опирается на EventCountInterval.
73.	snapshot_store.cs: Опечатка в комментарии // infrastructure/event_store/snapshot_store.c.
74.	event_stream.cs: В Append не устанавливается metadata.EventType (в отличие от AppendRange) – тип события останется пустым при одиночном добавлении.
75.	consistency_manager.cs: LockKeyFactory объявлен как partial class, но исходный класс (в distributed_lock.cs) не является partial – ошибка компиляции.
76.	consistency_manager.cs: CheckGlobalInvariantsAsync игнорирует CancellationToken, вызывая Load с CancellationToken.None.
77.	consistency_manager.cs: Дублирование using для EventStore (infrastructure.event_store и Infrastructure.EventStore) может вызвать неоднозначность.
78.	in_memory_bus.cs: SendAsync<TResult>(ICommand<TResult>) неработоспособен: пытается создать ICommandHandler<> с одним generic-параметром, а для команд с результатом нужен ICommandHandler<TCommand, TResult>, которого нет в проекте.
79.	in_memory_bus.cs: Subscribe<TCommand>(Func<TCommand, CommandContext?, Task>) пустой – делегаты-обработчики команд никогда не вызываются.
80.	in_memory_bus.cs: Subscribe<TEvent, THandler>() не работает – вызывает пустой приватный Subscribe.
81.	in_memory_bus.cs: Ошибки обработчиков глотаются (catch (Exception) { }) – скрывает проблемы.
82.	in_memory_bus.cs: Дублирование вызова обработчиков: DI-обработчики могут быть вызваны дважды (из _eventHandlers и из GetServices).
83.	in_memory_bus.cs: QueryPagedAsync использует некорректное приведение типов – может не работать для IPagedQuery<TResult>.
84.	rabbitmq_bus.cs: Синхронные блокировки (GetAwaiter().GetResult()) в конструкторе, Subscribe, Dispose – риск взаимоблокировок.
85.	rabbitmq_bus.cs: Утечка очередей при Unsubscribe – не закрывается потребитель и не удаляется очередь.
86.	rabbitmq_bus.cs: Создание новой очереди на каждую подписку – неэффективно, множество очередей.
87.	rabbitmq_bus.cs: Ошибки при обработке сообщений приводят к Nack с requeue: false, т.е. сообщения теряются без повторной обработки.
88.	rabbitmq_bus.cs: Subscribe<TEvent>(Func<...>) нельзя отписать – нет метода отписки для делегата.
89.	rabbitmq_bus.cs: PublishAsync(IDomainEvent @event, CommandContext context, ...) игнорирует context.
90.	rabbitmq_bus.cs: Пространства имён: использование dnd_game.infrastructure.message_bus (нижний) в using вместе с dnd_game.Infrastructure.MessageBus (верхний) для namespace – путаница.
91.	auth_provider.cs: GetUserContextFromTokenAsync не заполняет OwnedCharacterIds, возвращает пустой список – пользователь не сможет получить доступ к своим персонажам.
92.	auth_provider.cs: Нет rate limiting на LoginAsync – возможен брутфорс.
93.	auth_provider.cs: ValidateTokenAsync возвращает true/false, но не обрабатывает исключения (например, при неправильном секрете).
94.	token_service.cs: При обновлении refresh-токена не вызывается RevokeRefreshTokenAsync после выдачи нового (в AuthProvider.RefreshTokenAsync вызывается, но в самом TokenService.RefreshAccessTokenAsync нет) – нужна ротация.
95.	token_service.cs: GenerateRefreshTokenAsync не удаляет старые токены пользователя, что может привести к накоплению.
96.	postgres_user_repository.cs / refresh_token_store.cs: Синхронная инициализация БД в конструкторе (conn.Open(), cmd.ExecuteNonQuery()) – блокирует поток и падает при недоступной БД.
97.	postgres_user_repository.cs / refresh_token_store.cs: UpdateAsync не обновляет индексы _usernameIndex/_emailIndex при изменении username/email (в InMemoryUserRepository).
98.	postgres_user_repository.cs / refresh_token_store.cs: Отсутствует CancellationToken в интерфейсе IUserRepository (в PostgresUserRepository он есть в некоторых методах, но не во всех).
99.	user_security_context_provider.cs: Синхронный вызов асинхронного метода GetByIdAsync(...).GetAwaiter().GetResult() – не рекомендуется, потенциально опасно.
100.	game_server.cs: Аутентификация заглушка: Guid.Parse(authRequest.Token) – без проверки подписи/срока, исключение не перехватывается.
101.	game_server.cs: HandleIncomingQuery не реализован – всегда ошибка.
102.	game_server.cs: WebSocket ReceiveLoop не поддерживает фрагментацию сообщений.
103.	game_server.cs: MaxConnectionsPerUser из конфигурации не используется.
104.	game_server.cs: GetAffectedSessions всегда возвращает null – события рассылаются всем.
105.	game_server.cs: Нет обработки ошибок при принятии подключений (AcceptWebSocketConnections/AcceptTcpConnections могут упасть и остановить цикл).
106.	game_server.cs: При отключении DisconnectClient вызывает CloseAsync с CancellationToken.None и пустым catch.
107.	session_manager.cs: RemoveConnection использует ConcurrentBag.TryTake для удаления конкретного соединения – удаляется случайный элемент, а не нужный (нужно заменить на ConcurrentDictionary<Guid, byte>).
108.	session_manager.cs: _connectionToUser хранит список List<Guid>, но при удалении не очищает его – возможна утечка.
109.	rate_limiter.cs: Отсутствие потокобезопасности в TokenBucket и SlidingWindow – гонки данных при параллельных вызовах.
110.	rate_limiter.cs: TryConsumeAsync игнорирует CancellationToken.
111.	rate_limiter.cs: Нет очистки устаревших ключей – возможен неограниченный рост словарей.
112.	network_protocol.cs: В MessageFlags значение QueryResponse = 12 некорректно (не степень двойки, возможно, ошибка).
113.	network_protocol.cs: В DeserializeByType не обрабатываются типы Query, QueryResponse, Undo/Redo, Ping/Pong, Disconnect – будет ArgumentOutOfRangeException.
114.	monster_ai.cs: Двойная проверка IsInMeleeRange – вторая всегда ложна, ветка MoveTowards недостижима.
115.	monster_ai.cs: Все персонажи считаются врагами – нет проверки фракций.
116.	perception_pipeline.cs: EstimateDistance всегда возвращает 30 футов, GetLightLevelAt всегда Bright, IsOnSameSurface всегда true – восприятие не работает реально.
117.	perception_pipeline.cs: Дублирование SenseType и LightLevel (уже есть в infrastructure/world/visibility_calculator.cs и infrastructure/ai/perception_pipeline.cs) – конфликт типов.
118.	script_engine.cs: ExecuteSingleCommand реализован только для SetVariable, остальные команды внутри блоков If/While игнорируются.
119.	script_engine.cs: ExecuteApplyCondition не отправляет команду – создаёт new ApplyCondition, но не вызывает commandBus.SendAsync.
120.	script_engine.cs: ExecuteChangeFactionReputation использует Guid.Empty как CampaignId.
121.	script_engine.cs: ExecuteRollSkillCheck всегда возвращает успех без реального броска.
122.	npc_behavior_tree.cs: RepeaterNode при maxRepeats = -1 и всегда успешном ребёнке вызывает бесконечный цикл внутри одного тика.
123.	npc_behavior_tree.cs: ParallelNode не поддерживает статус Running и не обрабатывает исключения дочерних задач.
124.	grid_provider.cs / visibility_calculator.cs: Дублирование IGridProvider – один в domain/interfaces, другой в infrastructure/world – оставить один.
125.	visibility_calculator.cs: ProcessVision ограничивает радиус 200 футов, в то время как HasLineOfSight допускает 1200 футов.
126.	visibility_calculator.cs: GetEffectiveLightAt возвращает освещение из Cell.Light, но в старом IGridProvider (domain) у GridCell нет такого свойства – возможна ошибка компиляции при использовании неправильного интерфейса.
127.	interactive_object.cs: GrantLoot вызывает commandBus.SendAsync без await – команды отправляются в фоне, ошибки теряются.
128.	interactive_object.cs: Проверка иммунитетов/сопротивлений через Contains для строки с запятой может давать ложные срабатывания (например, "cold" в "scold").
129.	distributed_lock.cs: AcquireAsync использует timeout как TTL, а не как время ожидания – Redis LockTake не ждёт освобождения, блокировка не работает как ожидается.
130.	distributed_lock.cs: LockHandle.Dispose блокирует поток через GetAwaiter().GetResult().
131.	distributed_lock.cs: ForceReleaseAsync не проверяет права – любой может снять блокировку.
132.	saga_coordinator.cs: Блокировка по SagaId, а не по CorrelationId – для саг, где SagaId может не совпадать (например, QuestSaga при CharacterDied), блокировка неэффективна.
133.	saga_coordinator.cs: Статус после компенсации устанавливается в Compensating, но не сохраняется Compensated.
134.	undo_manager.cs: При превышении лимита UndoStack пересоздаётся из списка, что может быть неэффективно, но порядок LIFO сохраняется.
135.	undo_manager.cs: IUndoableCommand и UndoableCommand дублируют функциональность IUndoableAction/UndoableActionBase – возможно, стоит унифицировать.
136.	health_check.cs: CheckMessageBusAsync не проверяет реальное соединение, просто возвращает Healthy, если _rabbitMqBus не null.
137.	health_check.cs: CheckEventStoreAsync вызывает GetEvents(Guid.Empty, 0), что может быть некорректной проверкой.
138.	metrics_collector.cs: Теги не применяются – AddTag сохраняет в AsyncLocal, но не используется при вызове IncrementCounter/RecordHistogram.
139.	tracer.cs: Дублирование типов SenseType/LightLevel (уже есть в AI и World) – путаница.
140.	Общее: Разнобой в пространствах имён: dnd_game.infrastructure.* (нижний регистр) vs dnd_game.Infrastructure.* (верхний) – много файлов используют разные варианты.
141.	Общее: Множество синхронных вызовов асинхронных методов (GetAwaiter().GetResult()) в конструкторах, Dispose, инициализации – риск взаимоблокировок.
142.	Общее: Отсутствие CancellationToken во многих методах, особенно в репозиториях и сервисах.
143.	Общее: Заглушки, которые должны быть реализованы (например, HandleIncomingQuery, GetAffectedSessions, TryConsumeAsync и т.д.).
144.	rest_api.cs: Критическая ошибка с атрибутами в CharactersController – [HttpGet("{id}/conditions")] стоит перед [HttpPost("{id}/conditions/clear")] и методом ClearAllConditions, из-за чего GetConditions остаётся без HTTP-атрибута. Нужно перенести [HttpGet] непосредственно перед GetConditions.
145.	rest_api.cs: Ошибка типов в CampaignController.GetQuests – используется (Application.Projections.QuestStatus?)parsedStatus без using dnd_game.Application.Projections; – добавить using или полностью квалифицировать.
146.	rest_api.cs: Использование доменной команды в теле POST – CharactersController.CreateCharacter принимает [FromBody] CreateCharacter command напрямую. Лучше использовать DTO CreateCharacterRequest, а затем мапить в команду.
147.	rest_api.cs: Дублирование using для message bus – присутствуют using dnd_game.infrastructure.message_bus; и using dnd_game.Infrastructure.MessageBus; – оставить один.
148.	rest_api.cs: GameControllerBase.SessionId не валидирует заголовок – при отсутствии/некорректном X-Session-Id возвращается Guid.Empty, что может привести к ошибкам контекста. Стоит добавить проверку.
149.	rest_api.cs: Дублирование OkOrNotFound – метод определён в двух контроллерах (CharactersController, CampaignController), лучше вынести в базовый класс.
150.	schemas.cs: Дублирование DTO с проекциями и контроллерами – многие DTO (CharacterDto, CombatStatusDto, CombatParticipantDto, QuestInfoDto) определены и здесь, и в Application.Projections/MaterializedViews, с разными сигнатурами – унифицировать.
151.	schemas.cs: ClearAllConditions определён как record ClearAllConditions(Guid id), а в контроллере используется ClearAllConditionsCommand – это разные типы. Переименовать в ClearAllConditionsRequest.
152.	schemas.cs: Дублирование request-типов – StartCraftingRequest, CancelCraftingRequest, StartDialogRequest, SelectOptionRequest, EndDialogRequest, ProposeTradeRequest и др. определены и в Schemas, и в контроллерах. Оставить только в Schemas.
153.	Контроллеры (CraftingController, DialogController, TradeController, TravelController): ICommandBus внедрён, но не используется во всех четырёх контроллерах.
154.	Контроллеры: Дублирование using message bus (та же проблема).
155.	TradeController: локально определён ProposeTradeRequest (дублируется с Schemas); отсутствует using dnd_game.Domain.Events; для TradeItem.
156.	TravelController: дублирование record-типов с Schemas; нет обработки исключений (в отличие от других контроллеров), что может привести к необработанным 500.
157.	websocket_handler.cs: Критическая ошибка в SubscribeToEvents – отписка не работает: в _eventSubscriptions добавляется Action, который повторно подписывается, а не отписывается. Нужно хранить делегаты для отписки или использовать IEventHandler<TEvent> с методом Unsubscribe.
158.	websocket_handler.cs: Рассылка событий без фильтрации – ShouldSendEventToSession возвращает true для событий без ISessionBoundEvent, т.е. все клиенты сессии получают все события. Нужно доработать фильтрацию.
159.	websocket_handler.cs: HandleCommand не регистрирует Undo – приведение commandObj is IUndoableAction никогда не срабатывает, т.к. доменные команды не реализуют этот интерфейс. Нужно либо добавить реализацию, либо убрать код.
160.	websocket_handler.cs: Два пути аутентификации – AuthenticateAsync не используется в HandleAsync; аутентификация выполняется через query-параметр. Следует удалить неиспользуемый метод или унифицировать.
161.	websocket_handler.cs: KeepAliveLoopAsync отправляет текст "ping" вместо WebSocket Ping-фрейма – клиент может не обработать. Использовать SendAsync с WebSocketMessageType.Text или специальный ping-фрейм.
162.	websocket_handler.cs: Нет ограничения максимального размера сообщения – ReceiveFullMessageAsync использует фиксированный буфер 4096, но не ограничивает размер MemoryStream, что может привести к переполнению памяти.
163.	websocket_handler.cs: CloseConnection не идемпотентна – при повторном вызове может повторно вызывать RemoveConnection и другие операции. Добавить проверку флага состояния.
164.	websocket_handler.cs: Устаревшие методы HandleCommandMessage и HandleQueryMessage не используются, их можно удалить.
165.	client_network.cs: UnregisterMessageHandler использует _handlers.TryTake(out _), удаляя случайный элемент – заменить на ConcurrentDictionary или список с блокировкой.
166.	client_network.cs: При переподключении не закрывается старый ClientWebSocket – возможна утечка соединений.
167.	client_network.cs: Нет таймаута аутентификации (в отличие от серверной стороны).
168.	client_network.cs: Создание нового JsonNetworkProtocol при каждой отправке/получении – лучше использовать singleton.
169.	client_network.cs: _receiveTask не очищается при переподключении, возможны накопившиеся задачи.
170.	input_handler.cs: Конструктор с logger = null и logger ?? throw new ArgumentNullException(nameof(logger)) – избыточно, можно просто объявить ILogger<InputHandler> logger.
171.	input_handler.cs: HandleExamine только возвращает сообщение, не отправляет команду/запрос.
172.	input_handler.cs: ProcessTargetSelection не сохраняет ожидаемую команду, пользователю приходится повторно вводить.
173.	input_handler.cs: HandleCastSpell парсинг int.Parse(args[2]) без проверки может упасть.
174.	input_handler.cs: BuildCommand не отправляет команду, только сообщение "ready" – возможно, ожидалась отправка.
175.	macros.cs: Подстановка переменных не работает – в ExecuteSendCommand параметры вида "$characterId" сериализуются как строки, но не подставляются значения из context.Variables. Нужно реализовать рекурсивную подстановку.
176.	macros.cs: SecondWind макрос – CommandParameters содержит { "Amount", "$secondwind_roll + $level" } – это строка, а не вычисленное выражение. Команда HealCharacter ожидает int, десериализация упадёт.
177.	macros.cs: fullattack макрос использует условие hasBonusAction == 1, но переменная hasBonusAction нигде не устанавливается.
178.	macros.cs: SelectTarget не реализован.
179.	macros.cs: EvaluateCondition предполагает числовое сравнение (Convert.ToDouble), но переменные могут быть нечисловыми.
180.	dm_ui.cs: IQueryBus внедрён, но не используется.
181.	dm_ui.cs: ManageQuests логика "toggle" неполная – не обрабатывает статусы Available/Failed.
182.	dm_ui.cs: Нет проверки прав пользователя – предполагается, что доступ только у DM, но в коде нет авторизации.
183.	dm_ui.cs: Использование Console.ReadLine() и int.Parse без обработки ошибок – может упасть при некорректном вводе.
184.	override_commands.cs: Критическая ошибка: ChangeFactionReputation – метод принимает Guid characterId и отправляет new ChangeFactionReputation(characterId, factionId, change), но команда ожидает CampaignId первым параметром. Необходимо исправить сигнатуру или передавать корректный campaignId.
185.	override_commands.cs: ResetCharacter вызывает ClearAllConditionsCommand безусловно, но в агрегате ClearAllConditions() бросает исключение, если нет состояний – может упасть.
186.	override_commands.cs: SetLevel использует LevelUpCharacter, который не позволяет понижать уровень – при newLevel < currentLevel будет исключение.
187.	override_commands.cs: SpawnMonster использует MoveCharacter для телепортации, но MoveCharacter интерпретирует координаты как целевую позицию (абсолютные). Возможно, ожидалось относительное перемещение.
188.	undo_manager.cs (presentation/dm_tools): RecordAndExecute не работает для большинства команд, т.к. они не реализуют IUndoableAction. Для реальной отмены нужно создать адаптеры обратных команд.
189.	undo_manager.cs: UndoActionById не реализован (заглушка).
190.	undo_manager.cs: ForceUndoLastPlayerAction не проверяет, что gmUserId действительно GM – полагается на внутреннюю проверку UndoManager, что может быть недостаточно.
191.	dependencies.cs: Множественные дублирования регистраций: InMemoryReplayEventStore, InMemoryTriggerDefinitionRepository, InMemoryRecipeRepository, IDialogueRepository, IQuestTrackingStore и др. зарегистрированы дважды.
192.	dependencies.cs: ISessionManager, IRateLimiter, UndoManager зарегистрированы дважды.
193.	dependencies.cs: Ручная регистрация контроллеров (AddScoped<CraftingController>() и т.д.) может конфликтовать с AddControllers().
194.	dependencies.cs: IOptions<HealthCheckOptions> может быть не зарегистрирован, если нет соответствующей конфигурационной секции.
195.	dependencies.cs: TokenSettings и JwtSettings – два разных класса, оба конфигурируются из разных секций; возможно, стоит объединить.
196.	constants.cs: Дублирование констант: MaxLevel, MaxAbilityScore, CriticalHitRoll, CriticalMissRoll, MaxConcentrationSpells, MaxAttunedItems, MaxFallDamageDice и др. уже определены в Domain.Rules (ValidationRules, CombatRules, MagicRules) и в Infrastructure.Config.GameRulesSettings.
197.	constants.cs: ExperienceThresholds полностью повторяет таблицу из LevelUpSaga и GameRulesSettings; DefaultGridSize = 20 не используется в проекте (в GridProvider по умолчанию 100x100).
198.	enums.cs: Практически все перечисления уже существуют в других слоях:
•	Terrain ↔ Infrastructure.World.CellTerrain
•	Ability ↔ Domain.ValueObjects.AbilityId
•	Skill ↔ Domain.ValueObjects.SkillId
•	DamageType ↔ строковые константы в ValidationRules и CombatRules
•	Condition ↔ Domain.ValueObjects.ConditionId
•	ActionType ↔ строки в CombatHandler / CombatRules
•	RestType ↔ строки "Short"/"Long" в RestRules
•	MagicSchool, CastingTime, SpellComponent ↔ Domain.Rules.MagicRules
•	ItemRarity, MagicItemType ↔ Application.Services.CraftingService (частично)
•	Sense ↔ Infrastructure.AI.SenseType и Infrastructure.World.SenseType
•	LightLevel ↔ Infrastructure.AI.LightLevel, Infrastructure.World.LightLevel, Domain.ValueObjects.LightLevel
•	QuestStatus ↔ Domain.Aggregates.QuestStatus и Application.Projections.QuestStatus
•	Attitude ↔ вычисляемое свойство FactionState.Attitude
•	CampaignRole, UserRole ↔ Application.Security.CampaignRole, Application.Security.UserRole
•	InteractiveObjectType ↔ Infrastructure.World.InteractiveObjectType
•	CoverType ↔ Infrastructure.World.VisibilityCalculator (частично)
Все эти дубликаты требуют конвертации и приводят к ошибкам компиляции, если одновременно использовать оба типа.
199.	exceptions.cs: Дублирование доменных исключений:
•	GameException ↔ Domain.Exceptions.DomainError
•	ValidationException ↔ Domain.Exceptions.InvalidAction / RuleViolation
•	RuleViolationException ↔ Domain.Exceptions.RuleViolation
•	NotFoundException ↔ Domain.Exceptions.EntityNotFoundException
•	ConcurrencyException ↔ Domain.Exceptions.StateConflictException
•	UnauthorizedException ↔ Domain.Exceptions.UnauthorizedActionException
•	InsufficientResourcesException ↔ Domain.Exceptions.InsufficientResourcesException
•	LimitExceededException – аналога нет, но его можно добавить в Domain.Exceptions
•	InvalidOperationForStateException ↔ Domain.Exceptions.InvalidAction
Проблема: GlobalExceptionHandler в Infrastructure.Exceptions обрабатывает только Domain.Exceptions, поэтому исключения из SharedKernel не будут преобразованы в корректные HTTP-ответы.
200.	primitives.cs: Дублирование базовых интерфейсов: IDomainEvent ↔ Domain.Events.IDomainEvent, ICommand ↔ Domain.Commands.ICommand, IQuery<TResult> ↔ Domain.Queries.IQuery<TResult>. Это особенно опасно: классы, реализующие доменные интерфейсы, не будут совместимы с SharedKernel и наоборот.
201.	primitives.cs: Дублирование AggregateRoot<TId> – доменный AggregateRoot не является обобщённым. Все агрегаты наследуют Domain.Aggregates.AggregateRoot, а не SharedKernel.AggregateRoot<TId>. Наличие обобщённой версии сбивает с толку и не используется.
202.	primitives.cs: Entity<TId> не используется – вся доменная модель построена на AggregateRoot (необобщённом) с Guid Id.
203.	primitives.cs: Result<T> и Maybe<T> не используются – проект не использует монадический подход. Эти типы остаются мёртвым кодом.
204.	primitives.cs: Отсутствие интеграции IRepository<T, in TId> – в проекте используются специфические репозитории, а не обобщённый интерфейс. Его наличие бесполезно.
205.	utils.cs: Дублирование функционала:
•	RollD20, RollD12, ... дублируют Dice value object (класс Dice уже умеет кидать и парсить).
•	RollWithAdvantage/RollWithDisadvantage дублируют D20RollHelper из Domain.ValueObjects.Dice.
•	AbilityModifier дублирует ModifierCalculator.Calculate.
•	PassiveCheck дублирует ModifierCalculator.PassiveSkill.
•	IsAttackHit дублирует CombatRules.IsHit.
•	FeetToSquares/SquaresToFeet дублируют Position.ChebyshevDistanceInFeet и т.д.
206.	utils.cs: Отсутствие контроля случайности – Random.Shared не позволяет задавать seed для тестирования. В игровых системах лучше использовать внедряемый Random или Dice с параметром.
207.	utils.cs: Неиспользуемые методы: Truncate, SanitizeName, RandomHexColor, UtcNow не используются в проекте; Clamp и Floor избыточны (есть Math.Clamp и Math.Floor).
208.	Миграции: Дублирование индекса idx_refresh_tokens_expires_at в 001_Initial.sql и 006_RefreshTokenIndexes.sql – оставить создание индекса только в одном файле (например, в 001), а из 006 убрать дублирующую строку.
209.	Миграции: Дублирование столбца gold в character_read_model – в 003 таблица уже создаётся с колонкой gold INT NOT NULL DEFAULT 0, а 005 делает ALTER TABLE – это избыточно. Удалить 005 или удалить колонку из 003.
210.	Миграции: Отсутствие внешних ключей – таблицы refresh_tokens.user_id, campaign_read_model.game_master_id, quest_participants.character_id и т.д. не имеют FOREIGN KEY. Это не критично, но нарушает ссылочную целостность.
211.	Миграции: Возможная проблема с порядком миграций – если 005_AddGoldToCharacterReadModel.sql применяется после 003, он сработает только если колонки gold нет. При чистой установке она уже есть – скрипт ничего не изменит, но запутывает.
212.	Миграции: DatabaseMigrator.Migrate() – синхронный метод, хотя DbUp поддерживает асинхронный API (PerformUpgradeAsync()). При старте приложения лучше не блокировать поток – добавить асинхронную перегрузку Task<bool> MigrateAsync(CancellationToken cancellationToken = default).
213.	Миграции: Путь к папке миграций захардкожен (Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrations")). В сценариях контейнеров или публикации папка может называться иначе – добавить параметр конфигурации.
214.	Миграции: Отсутствие проверки существования БД в DatabaseMigrator – EnsureDatabase.For.PostgresqlDatabase пытается создать базу, но если у пользователя нет прав, метод может упасть с исключением. Выводить более подробную информацию об ошибке (ex.ToString()).
215.	Миграции: Дублирование индексов в 001_Initial.sql – индекс idx_refresh_tokens_expires_at уже объявлен, и затем снова в 006 (уже отмечено в п.208).
216.	Миграции: В 001_Initial.sql поля user_id и session_id имеют NOT NULL – при сохранении событий из PostgresEventStore используются Guid.Empty как заглушки. Это допустимо, но если в будущем появятся события без пользователя/сессии, запись не удастся – можно разрешить NULL.
217.	Миграции: Отсутствует таблица для saga_state – в проекте есть ISagaStateRepository и InMemorySagaStateRepository, но в миграциях нет таблицы для персистентного хранения состояний саг. Добавить миграцию с таблицей saga_states (saga_id, correlation_id, status, version, created_at, updated_at, data JSONB).
218.	auth.js: Ошибка в authFetch при повторных запросах после 401 – в очереди pendingQueue сохраняются объекты { resolve, reject, opts }, но не сохраняется URL. При успешном обновлении токена выполняется fetch(opts.url, opts), где opts.url равен undefined – добавить url в объект очереди.
219.	auth.js: Переопределение logout может привести к неожиданному поведению – после объявления function logout() идёт const originalLogout = logout; logout = function () {...}. Это может конфликтовать в строгом режиме – оставить только одно определение.
220.	auth.js: Двойная инициализация темы – auth.js и ui-helpers.js оба подписываются на DOMContentLoaded и вызывают applyTheme – оставить инициализацию только в одном месте (лучше в ui-helpers.js).
221.	auth.js: requireRole останавливает выполнение скрипта только исключением – если роль не подходит, выводится сообщение, но после throw new Error скрипт страницы прекращается. Это нормально, но если есть код после requireRole, он не выполнится. Можно оставить как есть.
222.	ui-helpers.js: showToast не проверяет существование document.body при раннем вызове – если вызвать до загрузки DOM, будет ошибка. Обычно вызывается после, но не гарантировано.
223.	ui-helpers.js: showLoading создаёт только один оверлей – при множественных вызовах возвращается тот же, но при параллельных запросах может быть скрыт раньше времени. Не критично.
224.	characters.html: Создание персонажа – отправляет POST /api/characters с телом { name, maxHitPoints: 10 }, но серверный CreateCharacter ожидает CharacterId (Guid) обязательным. Исправление: либо сервер должен принимать DTO CreateCharacterRequest и генерировать ID, либо клиент должен генерировать characterId и включать его в JSON.
225.	character.html: Отображение персонажа – использует c.class, c.experience, c.strength, c.dexterity, но серверный CharacterDto содержит className, experiencePoints, abilityScores (словарь) и не имеет плоских полей силы/ловкости. Обновить JS или изменить DTO на сервере.
226.	trade.html: Регистр полей – отправляет fromCharacterId, toCharacterId и т.д., а серверный ProposeTradeRequest объявлен с PascalCase (FromCharacterId). ASP.NET Core обычно нечувствителен к регистру, но если отключено – будет ошибка. Уточнить настройки JSON или отправлять точные имена.
227.	register.html: Роль Admin – в форме есть <option value="Admin">Администратор сервера</option>, но сервер AuthProvider намеренно игнорирует Admin и откатывает к Player – удалить Admin из селектора.
228.	game.html: Отправка команд через WebSocket – клиент вызывает wsClient.sendCommand(cmd.commandType, cmd.commandJson, cmd.correlationId), но в зависимости от реализации websocket-client.js сигнатура может отличаться. Также wsClient.send({ type: 'chat', payload: { message: text } }) может не поддерживаться. Сверить с реальным API WebSocketClient.
229.	campaign.html / dm/campaign.html: Инлайн-обработчики с GUID – кнопки «Выбрать» создаются через onclick="document.getElementById('questIdInput').value='${q.questId}'". Если questId содержит кавычки (маловероятно), возможна XSS. Использовать data-атрибуты и addEventListener.
230.	Отсутствует файл websocket-client.js – на странице game.html подключён /js/websocket-client.js, но в предоставленных файлах его нет. Без него функциональность не работает.
231.	login.html предзаполненные testuser / 123456 – небезопасно для production. Убрать значения по умолчанию.
232.	Дублирование record-типов между schemas.cs и контроллерами – хотя это не в wwwroot, но влияет на фронтенд. Лучше унифицировать DTO.
233.	CSS site.css не содержит стилей для .loading-overlay и .toast-container – если страница подключает только site.css, а не custom.css, индикатор загрузки и уведомления не будут отображаться. Убедиться, что на всех страницах подключён custom.css.

