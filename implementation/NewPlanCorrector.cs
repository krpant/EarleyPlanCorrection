using PlanRecognitionNETF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static PlanRecognitionExtension.EarleyParser;
using static PlanRecognitionExtension.PartialObservabilityEarleyParser;
using static PlanRecognitionExtension.PartialObservabilityEarleyParser.POQueueItem;

namespace PlanRecognitionExtension
{
    internal class NewPlanCorrector : PartialObservabilityEarleyParser
    {
        // Properties:
        public int TotalNumberOfParsingStatesCreated { get; private set; } = 0;
        public DictionaryOfCreatedStatesByFinishedAbstractSubtasks AllCreatedStatesByFinishedAbstractSubtasks { get; } = new(); // for completer
        public HashSet<ICorrectionQueueItem> AllCreatedStates { get; } = [];
        public Dictionary<ICorrectionQueueItem, int> LowestInheritedCostPerItem { get; } = [];

        public class DictionaryOfCreatedStatesByFinishedAbstractSubtasks : Dictionary<CompletedSubtaskKey, HashSet<ICorrectionQueueItem>>
        {
            public void Add(ICorrectionQueueItem item)
            {
                for (int i = 0; i < item.EndIndices.Count; i++)
                {
                    if (item.CFGRule.Subtasks[i] is AbstractTask abstractTask)
                    {
                        var key = new CompletedSubtaskKey(i == 0 ? item.StartIndex : item.EndIndices[i - 1], item.EndIndices[i],
                            abstractTask.Task.TaskType);
                        if (!ContainsKey(key))
                        {
                            this[key] = new();
                        }
                        this[key].Add(item);
                    }
                }
            }
        }
        public class CompletedSubtaskKey : IEquatable<CompletedSubtaskKey>
        {
            public CompletedSubtaskKey(int startIndex, int endIndex, TaskType taskType)
            {
                StartIndex = startIndex;
                EndIndex = endIndex;
                TaskType = taskType;
            }

            int StartIndex { get; }
            int EndIndex { get; }
            TaskType TaskType { get; }

            public override bool Equals(object obj)
            {
                return Equals(obj as CompletedSubtaskKey);
            }

            public bool Equals(CompletedSubtaskKey other)
            {
                return other is not null &&
                       StartIndex == other.StartIndex &&
                       EndIndex == other.EndIndex &&
                       TaskType.Equals(other.TaskType);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(StartIndex, EndIndex, TaskType.GetHashCode());
            }
        }

        // Constructors:
        public NewPlanCorrector(List<TaskType> allTaskTypes, List<ActionType> allActionTypes,
            List<Constant> allConstants, List<ConstantType> allConstantTypes,
            List<Term> initialState, bool actionInsertionAllowed = true,
            bool actionDeletionAllowed = true, bool anytimeGoals = true,
            bool returnFirstResult = false) : base(allTaskTypes, allActionTypes, allConstants,
                allConstantTypes, initialState, actionInsertionAllowed, actionDeletionAllowed,
                anytimeGoals, returnFirstResult)
        {
        }

        // Base class overriden methods:

        protected override void CheckGoalChanges(PriorityQueue<POQueueItem.QueueItemGroundingEnumerator, int> candidateGoalsByNumberOfFlaws, ref Dictionary<POQueueItem, int> foundCandidateGoalItemsWithLastVersion, AbstractTask dummyStartingTask, CancellationToken cancellationToken, Dictionary<int, POQueueItem.QueueItemGroundingEnumerator> pendingEnumerators)
        {
            var insertedQueueItems = new HashSet<POQueueItem>(foundCandidateGoalItemsReachingMaxDepthDuringLastGrounding);
            base.CheckGoalChanges(candidateGoalsByNumberOfFlaws, ref foundCandidateGoalItemsWithLastVersion, dummyStartingTask, 
                cancellationToken, pendingEnumerators);
            Dictionary<POQueueItem, int> newSet = [];
            foreach (var item in foundCandidateGoalItemsWithLastVersion)
            {
                if (!insertedQueueItems.Contains(item.Key))
                {
                    newSet[item.Key] = item.Key.Version;
                    if (item.Key.Version != item.Value)
                    {
                        item.Key.IsPossibleGoal(InputPlanPart.Count, out int minNumberOfFlaws, dummyStartingTask, this);

                        QueueItemGroundingEnumerator groundingEnumerator = new(new(item.Key), this,
                            new(),
                            cancellationToken);
                        candidateGoalsByNumberOfFlaws.Enqueue(groundingEnumerator, minNumberOfFlaws);
                        if (!pendingEnumerators.ContainsKey(groundingEnumerator.Root.ID))
                        {
                            pendingEnumerators.Add(groundingEnumerator.Root.ID, groundingEnumerator);
                        }
                        else
                        {
                            pendingEnumerators[groundingEnumerator.Root.ID] = groundingEnumerator;
                        }
                    }
                }
            }
            foundCandidateGoalItemsWithLastVersion = newSet;
        }

        protected override bool FixedPrefix(int nextIndexInPlanToTry)
        {
            return base.FixedPrefix(nextIndexInPlanToTry);
        }

        protected override PriorityQueueWatchingFlaws InitQueue(List<TaskType> allTaskTypes, out AbstractTask dummyStartingTask, List<Rule> allRules, List<HashSet<POQueueItem>> completedStatesByFirstAction, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Action> plan)
        {
            PriorityQueueWatchingFlaws queue = new();
            TaskType dummyStartingTaskType = new TaskType("start_dummy", 0);
            dummyStartingTask = new AbstractTask(new Task(dummyStartingTaskType));

            foreach (var taskType in allTaskTypes)
            {
                Rule dummyRule = new Rule
                {
                    MainTaskType = dummyStartingTaskType,
                    TaskTypeArray = new TaskType[] { taskType },
                    ArrayOfReferenceLists = new List<int>[1] { Enumerable.Range(0, taskType.NumOfVariables).ToList() },
                    MainTaskReferences = new List<int>(0)
                };

                CFGTask cFGSubtask = new AbstractTask(new Task(taskType));
                CFGRule dummyCFGRule = new CFGRule(dummyStartingTask, new CFGTask[] { cFGSubtask }, dummyRule, this);
                var queueItem = CreateNewQueueItemAndAddToTablesIfNotExistingOrWithHigherInheritedCost(dummyCFGRule, 0, 0, completedStatesByFirstAction, partiallyProcessedStatesByLastAction, plan, new());
                //POQueueItem queueItem = CreateQueueItemAndAddToTables(dummyCFGRule, 0, 0, completedStatesByFirstAction,
                //    partiallyProcessedStatesByLastAction, plan);
                queue.Enqueue(queueItem);
            }

            return queue;
        }

        // New methods:

        /// <summary>
        /// The item is created and added to the tables only if it does not exist yet. If it exists, returns null.
        /// </summary>
        /// <param name="cFGRule"></param>
        /// <param name="firstActionIndex"></param>
        /// <param name="lastActionIndex"></param>
        /// <param name="completedStatesByFirstAction"></param>
        /// <param name="partiallyProcessedStatesByLastAction"></param>
        /// <param name="plan"></param>
        /// <param name="endIndices"></param>
        /// <param name="numberOfFlaws"></param>
        /// <param name="coversActionInPrefix"></param>
        /// <returns></returns>
        internal POQueueItem CreateNewQueueItemAndAddToTablesIfNotExistingOrWithHigherInheritedCost(CFGRule cFGRule, int firstActionIndex, int lastActionIndex, List<HashSet<POQueueItem>> completedStatesByFirstAction,
            List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Action> plan, List<int> endIndices, int numberOfFlaws = 0, bool coversActionInPrefix = false)
        {
            if (!cFGRule.TryGetNextTask(out CFGTask nextTask))
            {
                CorrectionCompleterQueueItem result = new CorrectionCompleterQueueItem(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws,
                endIndices, this, coversActionInPrefix);

                if (AllCreatedStates.Contains(result)
                    && LowestInheritedCostPerItem[result] <= result.MinNumberOfFlawsBeforeThisDecomposition)
                {
                    return null;
                }
                (result as ICorrectionQueueItem).InitUniqueVersionID();
                AddNewStateToSetByFirstAction(completedStatesByFirstAction, result);
                LowestInheritedCostPerItem[result] = Math.Min(LowestInheritedCostPerItem.GetValueOrDefault(result, int.MaxValue), result.MinNumberOfFlawsBeforeThisDecomposition);
                TotalNumberOfParsingStatesCreated++;
                AllCreatedStatesByFinishedAbstractSubtasks.Add(result);
                AllCreatedStates.Add(result);
                return result;
            }

            else
            {
                POQueueItem result = null;

                if (nextTask is PrimitiveTask)
                {
                    result = new CorrectionScannerQueueItem(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws, plan, endIndices, this);
                }

                else if (nextTask is AbstractTask)
                {
                    result = new CorrectionPredictorQueueItem(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws, endIndices, this);
                }

                else
                {
                    Debugger.Break();
                }

                Debug.Assert(result != null);

                ICorrectionQueueItem resultAsICorrectionQueueItem = result as ICorrectionQueueItem;

                Debug.Assert(resultAsICorrectionQueueItem != null, $"The new queue item is not of the type {typeof(ICorrectionQueueItem)}.");

                if (result != null)
                {
                    foreach (var x in AllCreatedStates)
                    {
                        var b = x.Equals(resultAsICorrectionQueueItem);
                        int y = x.GetHashCode();
                        int y1 = x.CFGRule.GetHashCode();
                        int y2 = (x as POQueueItem).GetHashCode();
                        int y3 = HashCode.Combine(x.EndIndices.AsEnumerable);


                        int z = resultAsICorrectionQueueItem.GetHashCode();
                        int z1 = resultAsICorrectionQueueItem.CFGRule.GetHashCode();
                        int z2 = (resultAsICorrectionQueueItem as POQueueItem).GetHashCode();
                        int z3 = HashCode.Combine(resultAsICorrectionQueueItem.EndIndices.AsEnumerable);
                    }
                    if (AllCreatedStates.Contains(resultAsICorrectionQueueItem)
                        && LowestInheritedCostPerItem[resultAsICorrectionQueueItem] <= result.MinNumberOfFlawsBeforeThisDecomposition)
                    {
                        return null;
                    }
                    (result as ICorrectionQueueItem).InitUniqueVersionID();
                    LowestInheritedCostPerItem[resultAsICorrectionQueueItem] = Math.Min(LowestInheritedCostPerItem.GetValueOrDefault(resultAsICorrectionQueueItem, int.MaxValue), result.MinNumberOfFlawsBeforeThisDecomposition);
                    TotalNumberOfParsingStatesCreated++;
                    AllCreatedStatesByFinishedAbstractSubtasks.Add(result as ICorrectionQueueItem);
                    AddNewStateToSetByLastAction(partiallyProcessedStatesByLastAction, result);
                }
                else
                {
                    Debug.Assert(false, "The result is null.");
                }

                AllCreatedStates.Add(resultAsICorrectionQueueItem);
                return result;
            }
        }

        internal POQueueItem CreateNewQueueItem(CFGRule cFGRule, int firstActionIndex, int lastActionIndex, List<HashSet<POQueueItem>> completedStatesByFirstAction,
            List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Action> plan, List<int> endIndices, int numberOfFlaws = 0, bool coversActionInPrefix = false)
        {
            if (!cFGRule.TryGetNextTask(out CFGTask nextTask))
            {
                return new CorrectionCompleterQueueItem(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws,
                endIndices, this, coversActionInPrefix);
            }
            else if (nextTask is PrimitiveTask)
            {
                return new CorrectionScannerQueueItem(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws, plan, endIndices, this);
            }

            else if (nextTask is AbstractTask)
            {
                return new CorrectionPredictorQueueItem(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws, endIndices, this);
            }

            Debug.Assert(false);
            return null;
        }


        // Static methods:

        /// <summary>
        /// Returns false if item has been already previously created.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="partiallyProcessedStatesByLastAction"></param>
        /// <returns></returns>
        internal static bool IsNew(POQueueItem item, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction,
            List<HashSet<POQueueItem>> completedStatesByFirstAction)
        {
            POQueueItem existingItem = null;
            if (partiallyProcessedStatesByLastAction.Count > item.LastActionCoveredByThisRule)
            {
                partiallyProcessedStatesByLastAction[item.LastActionCoveredByThisRule].TryGetValue(item, out existingItem);
            }
            if (existingItem == null && completedStatesByFirstAction.Count > item.LastActionCoveredBeforeThisRule)
            {
                completedStatesByFirstAction[item.LastActionCoveredBeforeThisRule].TryGetValue(item, out existingItem);
            }

            return existingItem == null;
        }

        /// <summary>
        /// Returns true iff moreSpecific is a more specific instantiation than moreGeneral.
        /// </summary>
        /// <param name="constants1"></param>
        /// <param name="constants2"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private static bool IsMoreSpecific(Constant[] moreSpecific, Constant[] moreGeneral)
        {
            if (moreSpecific.Length != moreGeneral.Length)
            {
                throw new ArgumentException("The number of variables in the two actions is not the same.");
            }
            bool atLeastOneMoreSpecificVariable = false;
            for (int i = 0; i < moreSpecific.Length; i++)
            {
                if (moreGeneral[i] == null || moreGeneral[i].Name == null || moreGeneral[i].Name.Length == 0)
                {
                    if (moreSpecific[i] != null && moreSpecific[i].Name != null && moreSpecific[i].Name.Length > 0)
                    {
                        atLeastOneMoreSpecificVariable = true;
                    }
                    continue;
                }
                if (moreSpecific[i] == null || moreSpecific[i].Name == null || moreSpecific[i].Name.Length == 0)
                {
                    return false;
                }
                if (moreSpecific[i].Name != moreGeneral[i].Name)
                {
                    return false;
                }
            }
            return atLeastOneMoreSpecificVariable; // if false, the instantiations are exactly the same, so not more specific
        }

        internal static bool AddSubtaskCompletingRule(POQueueItem queueItem, int subtaskIndex, POQueueItem completingRule, PriorityQueueWatchingFlaws queue)
        {
            bool changed = false;
            int prevMinFlaws = queueItem.MinNumberOfFlaws;
            int previousNumberOfCompletingRules = queueItem.SubtaskCompletingRules[subtaskIndex].Count;

            if (!queueItem.SubtaskCompletingRules[subtaskIndex].TryGetValue(completingRule, out int version) ||
                version != completingRule.Version)
            {
                queueItem.SubtaskCompletingRules[subtaskIndex][completingRule] = completingRule.Version;
                changed = true;
            }

            if (completingRule.MinNumberOfFlaws < queueItem.MinNumberOfFlawsBySubtask[subtaskIndex])
            {
                queueItem.MinNumberOfFlaws -= queueItem.MinNumberOfFlawsBySubtask[subtaskIndex];
                queueItem.MinNumberOfFlawsBySubtask[subtaskIndex] = completingRule.MinNumberOfFlaws;
                queueItem.MinNumberOfFlaws += completingRule.MinNumberOfFlaws;
            }
            else if (previousNumberOfCompletingRules == 0)
            {
                queueItem.MinNumberOfFlawsBySubtask[subtaskIndex] = completingRule.MinNumberOfFlaws;
                queueItem.MinNumberOfFlaws += completingRule.MinNumberOfFlaws;
            }

            bool added = false;

            if (previousNumberOfCompletingRules < queueItem.SubtaskCompletingRules[subtaskIndex].Count)
            {
                added = true;
            }

            


            if (changed)
            {
                queueItem.ChangeVersion();
            }

            return changed;
        }

        // Classes:

        internal interface ICorrectionQueueItem
        {
            // Properties:
            List<int> EndIndices { get; }
            int StartIndex { get; }
            NewPlanCorrector Parser { get; }
            CFGRule CFGRule { get; }
            int? UniqueVersionID { get; }
            void InitUniqueVersionID();
            // Default methods:

        }

        internal class CorrectionScannerQueueItem : ScannerQueueItem, ICorrectionQueueItem
        {
            // Properties:
            public List<int> EndIndices { get; }
            public NewPlanCorrector Parser { get; }

            public int StartIndex => LastActionCoveredBeforeThisRule;
            int? ICorrectionQueueItem.UniqueVersionID => uniqueVersionID;
            int? uniqueVersionID = null;


            // Constructors + methods overriden from object:
            internal CorrectionScannerQueueItem(CFGRule cFGRule, int firstActionIndex, int lastActionIndex, int numberOfFlaws, List<Action> plan,
                List<int> endIndices, NewPlanCorrector parser) : base(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws, plan, parser)
            {
                Parser = parser;
                EndIndices = endIndices;
            }

            public override bool Equals(object obj)
            {
                bool result = obj is CorrectionScannerQueueItem item && base.Equals(obj) &&
                    EndIndices.SequenceEqual(item.EndIndices) && (uniqueVersionID is null || item.uniqueVersionID is null || uniqueVersionID == item.uniqueVersionID);

                return result;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;

                    hash = hash * 31 + base.GetHashCode();

                    foreach (var idx in EndIndices)
                    {
                        hash = hash * 31 + idx;
                    }

                    return hash;
                }
            }


            // Base class overriden methods:

            internal override bool AddSubtaskCompletingRule(int subtaskIndex, POQueueItem completingRule, PriorityQueueWatchingFlaws queue)
            {
                return NewPlanCorrector.AddSubtaskCompletingRule(this, subtaskIndex, completingRule, queue);
            }

            /// <summary>
            /// Inserts new action and then chooses all possible actions from plan (and deletes actions in between). 
            /// All descendant parsing states are created at once.
            /// </summary>
            /// <param name="queue"></param>
            /// <param name="plan"></param>
            /// <param name="allEmptyActions"></param>
            /// <param name="completedStatesByFirstAction"></param>
            /// <param name="partiallyProcessedStatesByLastAction"></param>
            /// <param name="allRules"></param>
            /// <param name="cFGRulesGeneratedByPredictorByStartingIndex"></param>
            /// <param name="parser"></param>
            /// <exception cref="NotImplementedException"></exception>
            internal override void Process(PriorityQueueWatchingFlaws queue, List<Action> plan, HashSet<Action> allEmptyActions, List<HashSet<POQueueItem>> completedStatesByFirstAction, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Rule> allRules, List<HashSet<POQueueItem>> cFGRulesGeneratedByPredictorByStartingIndex, PartialObservabilityEarleyParser parser)
            {
                if (Parser.FixedPrefix(LastActionCoveredBeforeScanning + 1)) // plan repair
                {
                    // choose action from plan if possible
                    throw new NotImplementedException();
                }
                else
                {
                    // 1) insert new action:
                    if (parser.ALLOW_INSERTING_NEW_ACTIONS)
                    {
                        ICorrectionQueueItem newQueueItem = InsertNewAction(queue, allEmptyActions, completedStatesByFirstAction, partiallyProcessedStatesByLastAction, plan) as ICorrectionQueueItem;

                        Debug.Assert(newQueueItem != null, "The new queue item is null."); // Should not be null, should not already exist.
                    }

                    // 2) choose all possible actions from plan (and delete actions in between):
                    for (int i = LastActionCoveredBeforeScanning + 1; i <= plan.Count; i++)
                    {
                        bool actionFound = SetNextActionInPlanIfSuitable(plan);
                        if (actionFound)
                        {
                            ICorrectionQueueItem newQueueItem = IdentifyActionWithNextPlanAction(queue, completedStatesByFirstAction, partiallyProcessedStatesByLastAction, plan) as ICorrectionQueueItem;

                        }
                        if (!parser.ALLOW_DELETING_ACTIONS)
                        {
                            break;
                        }
                        SkipAction();
                    }
                }
            }

            protected override POQueueItem FinishScanningWithAction(Action action, int newNumberOfFlaws, int lastCoveredAction, PriorityQueueWatchingFlaws queue,
                List<HashSet<POQueueItem>> completedStatesByFirstAction, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Action> plan,
                bool coversActionInPrefix = false)
            {
                CFGRule cFGRule = CloneAndFillVarsBySubtaskInstantiation(CFGRule, action.ActionInstance,
                    CFGRule.CurrentSubtaskIndex, parser);
                cFGRule.IncrementCurrentSubtaskIndex();
                POQueueItem newQueueItem = Parser.CreateNewQueueItemAndAddToTablesIfNotExistingOrWithHigherInheritedCost(cFGRule, LastActionCoveredBeforeThisRule, lastCoveredAction,
                    completedStatesByFirstAction, partiallyProcessedStatesByLastAction, plan, [.. EndIndices, lastCoveredAction], newNumberOfFlaws, coversActionInPrefix);

                if (newQueueItem != null)
                {
                    newQueueItem.InitMinNumberOfFlawsBeforeThisDecomposition(MinNumberOfFlawsBeforeThisDecomposition);
                    CompletedPredictionChildren.Add(newQueueItem);
                    queue.Enqueue(newQueueItem);
                }

                return newQueueItem;
            }

            // Other methods:

            public void InitUniqueVersionID()
            {
                uniqueVersionID = ID;
            }
        }

        internal class CorrectionPredictorQueueItem : PredictorQueueItem, ICorrectionQueueItem
        {
            // Properties:
            public List<int> EndIndices { get; }
            public NewPlanCorrector Parser { get; }
            public int StartIndex => LastActionCoveredBeforeThisRule;
            int? ICorrectionQueueItem.UniqueVersionID => uniqueVersionID;
            int? uniqueVersionID = null;


            // Constructors + methods overriden from object:
            internal CorrectionPredictorQueueItem(CFGRule cFGRule, int firstActionIndex, int lastActionIndex, int numberOfFlaws,
                List<int> endIndices, NewPlanCorrector parser) : base(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws)
            {
                EndIndices = endIndices;
                Parser = parser;
            }

            public override bool Equals(object obj)
            {
                bool result = obj is CorrectionPredictorQueueItem item && base.Equals(obj) &&
                    EndIndices.SequenceEqual(item.EndIndices) && (uniqueVersionID is null || item.uniqueVersionID is null || uniqueVersionID == item.uniqueVersionID);
                return result;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;

                    hash = hash * 31 + base.GetHashCode();

                    foreach (var idx in EndIndices)
                    {
                        hash = hash * 31 + idx;
                    }

                    return hash;
                }
            }

            // Base class overriden methods:
            internal override bool AddSubtaskCompletingRule(int subtaskIndex, POQueueItem completingRule, PriorityQueueWatchingFlaws queue)
            {
                return NewPlanCorrector.AddSubtaskCompletingRule(this, subtaskIndex, completingRule, queue);
            }

            internal override void Process(PriorityQueueWatchingFlaws queue, List<Action> plan, HashSet<Action> allEmptyActions, List<HashSet<POQueueItem>> completedStatesByLastActionBefore, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction,
                List<Rule> allRules, List<HashSet<POQueueItem>> cFGRulesGeneratedByPredictorByStartingIndex, PartialObservabilityEarleyParser parser)
            {
                // 1) find existing completions:
                CFGRule.TryGetNextTask(out CFGTask nextTask);

                if (completedStatesByLastActionBefore.Count > LastActionCoveredByThisRule)
                {
                    List<HashSet<POQueueItem>> newCompletedStatesByLastActionBefore = [];
                    HashSet<POQueueItem> newQueueItems = [];
                    foreach (var completedRule in completedStatesByLastActionBefore[LastActionCoveredByThisRule].OrderBy(x => x.MinNumberOfFlaws))
                    {
                        if (CFGTask.SameTypeTasks(completedRule.CFGRule.MainTask, nextTask)
                            && !IsMoreSpecific(nextTask.GetConstants(), completedRule.CFGRule.MainTask.GetConstants()))
                        {
                            PredictionChildren.Add(completedRule);

                           
                            List<HashSet<POQueueItem>> newPartiallyProcessedStatesByLastAction = new();
                            for (int i = 0; i < partiallyProcessedStatesByLastAction.Count; i++)
                            {
                                newPartiallyProcessedStatesByLastAction.Add(new
                                    HashSet<POQueueItem>(partiallyProcessedStatesByLastAction[i]));
                            }


                            int prevNumberOfStates = Parser.TotalNumberOfParsingStatesCreated;
                            POQueueItem newQueueItem = (completedRule as CorrectionCompleterQueueItem).ProcessPredictorItem(
                                this, newPartiallyProcessedStatesByLastAction, newCompletedStatesByLastActionBefore, queue,
                                plan, allEmptyActions, completedStatesByLastActionBefore, partiallyProcessedStatesByLastAction,
                                allRules, cFGRulesGeneratedByPredictorByStartingIndex, parser, true);

                            Debug.Assert(newQueueItem == null || newQueueItem is ICorrectionQueueItem, $"The new queue item is not of the type {typeof(ICorrectionQueueItem)}.");
                            Debug.Assert(Parser.TotalNumberOfParsingStatesCreated == prevNumberOfStates || Parser.TotalNumberOfParsingStatesCreated == prevNumberOfStates + 1,
                                $"The number of parsing states created should be {prevNumberOfStates} or {prevNumberOfStates + 1}.");

                            if (prevNumberOfStates < Parser.TotalNumberOfParsingStatesCreated)
                            {
                                Debug.Assert(newQueueItem != null, $"The new queue item is null.");

                                newQueueItems.Add(newQueueItem);
                            }
                            
                            partiallyProcessedStatesByLastAction.Clear();
                            for (int i = 0; i < newPartiallyProcessedStatesByLastAction.Count; i++)
                            {
                                partiallyProcessedStatesByLastAction.Add(new HashSet<POQueueItem>(
                                    newPartiallyProcessedStatesByLastAction[i]));
                            }

                        }
                    }
                    foreach (var newQueueItem in newQueueItems)
                    {
                        queue.Enqueue(newQueueItem);
                    }
                    AddStatesToSetByFirstAction(newCompletedStatesByLastActionBefore,
                        completedStatesByLastActionBefore);
                }

                // 2) create new predictions:
                TaskType desiredTaskType = (nextTask as AbstractTask).Task.TaskType;
                foreach (var rule in allRules)
                {
                    if (rule.MainTaskType.Equals(desiredTaskType))
                    {
                        CFGTask[] subtasks = GetSubtasksForRule(nextTask as AbstractTask, rule, CFGRule);
                        CFGRule cFGRule = new(nextTask.Clone() as AbstractTask, subtasks, rule, parser);

                        POQueueItem queueItem = Parser.CreateNewQueueItemAndAddToTablesIfNotExistingOrWithHigherInheritedCost(cFGRule, LastActionCoveredByThisRule, LastActionCoveredByThisRule,
                            completedStatesByLastActionBefore, partiallyProcessedStatesByLastAction, plan, new());

                        if (queueItem != null)
                        {
                            ICorrectionQueueItem correctionQueueItem = queueItem as ICorrectionQueueItem;
                            Debug.Assert(correctionQueueItem != null, $"The new queue item is not of the type {typeof(ICorrectionQueueItem)}.");

                            queueItem.InitMinNumberOfFlawsBeforeThisDecomposition(TotalMinNumberOfFlaws);

                            while (cFGRulesGeneratedByPredictorByStartingIndex.Count <= LastActionCoveredByThisRule)
                            {
                                cFGRulesGeneratedByPredictorByStartingIndex.Add(new HashSet<POQueueItem>());
                            }
                            cFGRulesGeneratedByPredictorByStartingIndex[LastActionCoveredByThisRule].Add(queueItem);

                            PredictionChildren.Add(queueItem);

                            queue.Enqueue(queueItem);
                        }
                       
                    }
                }
            }

            // Other methods:

            public void InitUniqueVersionID()
            {
                uniqueVersionID = ID;
            }
        }

        internal class CorrectionCompleterQueueItem : CompleterQueueItem, ICorrectionQueueItem
        {
            // Properties:
            public List<int> EndIndices { get; }
            public NewPlanCorrector Parser { get; }
            public int StartIndex => LastActionCoveredBeforeThisRule;

            int? ICorrectionQueueItem.UniqueVersionID => uniqueVersionID;
            int? uniqueVersionID = null;


            // Constructors + methods overriden from object:
            internal CorrectionCompleterQueueItem(CFGRule cFGRule, int firstActionIndex, int lastActionIndex, int numberOfFlaws,
                List<int> endIndices, NewPlanCorrector parser, bool coversActionInPrefix = false) : base(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws, coversActionInPrefix)
            {
                EndIndices = endIndices;
                Parser = parser;
            }
            public override bool Equals(object obj)
            {
                bool result = obj is CorrectionCompleterQueueItem item && base.Equals(obj) &&
                    EndIndices.SequenceEqual(item.EndIndices) && (uniqueVersionID is null || item.uniqueVersionID is null || uniqueVersionID == item.uniqueVersionID);
                return result;
            }


            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;

                    hash = hash * 31 + base.GetHashCode();

                    foreach (var idx in EndIndices)
                    {
                        hash = hash * 31 + idx;
                    }

                    return hash;
                }
            }

            // Base class overriden methods:
            internal override bool AddSubtaskCompletingRule(int subtaskIndex, POQueueItem completingRule, PriorityQueueWatchingFlaws queue)
            {
                return NewPlanCorrector.AddSubtaskCompletingRule(this, subtaskIndex, completingRule, queue);
            }

            internal override void Process(PriorityQueueWatchingFlaws queue, List<Action> plan, HashSet<Action> allEmptyActions, List<HashSet<POQueueItem>> completedStatesByFirstAction,
                List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Rule> allRules, List<HashSet<POQueueItem>> cFGRulesGeneratedByPredictorByStartingIndex, PartialObservabilityEarleyParser parser)
            {
                // 1) Create new states by classical completer:

                while (completedStatesByFirstAction.Count <= LastActionCoveredBeforeThisRule)
                {
                    completedStatesByFirstAction.Add(new HashSet<POQueueItem>());
                }
                completedStatesByFirstAction[LastActionCoveredBeforeThisRule].Add(this);

                List<HashSet<POQueueItem>> newPartiallyProcessedStates = new();
                List<HashSet<POQueueItem>> newCompletedStates = new();

                foreach (var item in partiallyProcessedStatesByLastAction[LastActionCoveredBeforeThisRule]) //.OrderBy(x => x.TotalMinNumberOfFlaws)
                {
                    if (item.CFGRule.TryGetNextTask(out CFGTask nextTask) && CFGTask.SameTypeTasks(nextTask, CFGRule.MainTask) &&
                        !IsMoreSpecific(nextTask.GetConstants(), CFGRule.MainTask.GetConstants())
                        && MinNumberOfFlawsBeforeThisDecomposition <= item.TotalMinNumberOfFlaws)
                    {
                        List<HashSet<POQueueItem>> newPartiallyProcessedStatesByLastAction = new();
                        for (int i = 0; i < partiallyProcessedStatesByLastAction.Count; i++)
                        {
                            newPartiallyProcessedStatesByLastAction.Add(new
                                HashSet<POQueueItem>(partiallyProcessedStatesByLastAction[i]));
                        }
                        int prevNumberOfStates = Parser.TotalNumberOfParsingStatesCreated;
                        POQueueItem newQueueItem = ProcessPredictorItem(item, newPartiallyProcessedStates, newCompletedStates,
                        queue, plan, allEmptyActions, completedStatesByFirstAction, newPartiallyProcessedStatesByLastAction,
                        allRules, cFGRulesGeneratedByPredictorByStartingIndex, parser, true);
                        Debug.Assert(newQueueItem == null || newQueueItem is ICorrectionQueueItem, $"The new queue item is not of the type {typeof(ICorrectionQueueItem)}.");
                        Debug.Assert(Parser.TotalNumberOfParsingStatesCreated == prevNumberOfStates || Parser.TotalNumberOfParsingStatesCreated == prevNumberOfStates + 1,
                                $"The number of parsing states created should be {prevNumberOfStates} or {prevNumberOfStates + 1}.");

                        if (prevNumberOfStates < Parser.TotalNumberOfParsingStatesCreated)
                        {
                            Debug.Assert(newQueueItem != null);
                            queue.Enqueue(newQueueItem);
                        }

                        partiallyProcessedStatesByLastAction.Clear();
                        for (int i = 0; i < newPartiallyProcessedStatesByLastAction.Count; i++)
                        {
                            partiallyProcessedStatesByLastAction.Add(new HashSet<POQueueItem>(
                                newPartiallyProcessedStatesByLastAction[i]));
                        }
                    }
                }

                AddStatesToSetByLastAction(newPartiallyProcessedStates, partiallyProcessedStatesByLastAction);
                AddStatesToSetByFirstAction(newCompletedStates, completedStatesByFirstAction);

                // 2) Add the new completion to existing states:
                var key = new CompletedSubtaskKey(LastActionCoveredBeforeThisRule, LastActionCoveredByThisRule, CFGRule.MainTask.Task.TaskType);
                foreach (var item in Parser.AllCreatedStatesByFinishedAbstractSubtasks.GetValueOrDefault(key, []))
                {
                    POQueueItem pOQueueItem = item as POQueueItem;
                    for (int i = 0; i < item.EndIndices.Count; i++) // only for completed subtasks
                    {
                        if (pOQueueItem.SubtaskCompletingRules.Length > i && pOQueueItem.SubtaskCompletingRules[i].Count > 0 // Completed subtask
                            && CFGTask.SameTypeTasks(pOQueueItem.CFGRule.Subtasks[i], CFGRule.MainTask)
                           
                            && (i == 0 && LastActionCoveredBeforeThisRule == pOQueueItem.LastActionCoveredBeforeThisRule || i > 0
                                && item.EndIndices[i - 1] == LastActionCoveredBeforeThisRule)
                            && item.EndIndices[i] == LastActionCoveredByThisRule
                            && !IsMoreSpecific(CFGRule.MainTask.GetConstants(), pOQueueItem.CFGRule.Subtasks[i].GetConstants())
                            && MinNumberOfFlawsBeforeThisDecomposition <=
                            pOQueueItem.MinNumberOfFlawsBeforeThisDecomposition + pOQueueItem.MinNumberOfFlawsBySubtask.Take(i).Sum())
                        {
                            bool decreaseCostInQueue = TotalMinNumberOfFlaws < pOQueueItem.MinNumberOfFlawsBySubtask[i];
                            int previousFlaws = pOQueueItem.TotalMinNumberOfFlaws;

                            RulesCompletedByThisRule.Add(new(pOQueueItem, i));
                            pOQueueItem.AddSubtaskCompletingRule(i, this, queue);

                            if (decreaseCostInQueue)
                            {
                                Debug.Assert(queue.IsEnqueued(pOQueueItem));
                                queue.Decrease(pOQueueItem, previousFlaws);
                            }
                        }

                    }
                }
            }

            /// <summary>
            /// Processes the predictor item similarly to the base class, but does not insert it to the queue.
            /// </summary>
            /// <param name="item"></param>
            /// <param name="newPartiallyProcessedStates"></param>
            /// <param name="newCompletedStates"></param>
            /// <param name="queue"></param>
            /// <param name="plan"></param>
            /// <param name="allEmptyActions"></param>
            /// <param name="completedStatesByFirstAction"></param>
            /// <param name="partiallyProcessedStatesByLastAction"></param>
            /// <param name="allRules"></param>
            /// <param name="cFGRulesGeneratedByPredictorByStartingIndex"></param>
            /// <param name="parser"></param>
            internal override POQueueItem ProcessPredictorItem(POQueueItem item, List<HashSet<POQueueItem>> newPartiallyProcessedStates, List<HashSet<POQueueItem>> newCompletedStates, PriorityQueueWatchingFlaws queue, List<Action> plan,
                HashSet<Action> allEmptyActions, List<HashSet<POQueueItem>> completedStatesByFirstAction, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Rule> allRules, List<HashSet<POQueueItem>> cFGRulesGeneratedByPredictorByStartingIndex,
                PartialObservabilityEarleyParser parser, bool onlyNewItemDontUpdateExisting = false)
            {
                CFGRule newCFGRule = CloneAndFillVarsBySubtaskInstantiation(item.CFGRule, CFGRule.MainTask.Task.TaskInstance,
                item.CFGRule.CurrentSubtaskIndex, parser);
                newCFGRule.IncrementCurrentSubtaskIndex();

                POQueueItem newQI = Parser.CreateNewQueueItemAndAddToTablesIfNotExistingOrWithHigherInheritedCost(newCFGRule, item.LastActionCoveredBeforeThisRule, LastActionCoveredByThisRule,
                    newCompletedStates, newPartiallyProcessedStates, plan, new List<int>((item as ICorrectionQueueItem).EndIndices).Append(LastActionCoveredByThisRule).ToList(), item.TotalMinNumberOfFlaws + this.MinNumberOfFlaws);


                if (newQI != null) // The state has not existed before
                {
                    PredictorItemsCompletedByThisRule.Add(item);
                    newQI.InitMinNumberOfFlawsBeforeThisDecomposition(item.MinNumberOfFlawsBeforeThisDecomposition);
                    newQI.CopySubtaskCompletingRulesFrom(item, queue);
                    RulesCompletedByThisRule.Add(new(newQI, item.CFGRule.CurrentSubtaskIndex));

                    newQI.AddSubtaskCompletingRule(item.CFGRule.CurrentSubtaskIndex, this, queue);
                    (item as PredictionQueueItem).CompletedPredictionChildren.Add(newQI);

                    // All newly created states will be enqueued at once:
                    
                    return newQI;
                }
                else if (!onlyNewItemDontUpdateExisting)
                {
                    PredictorItemsCompletedByThisRule.Add(item);
                    newQI = Parser.CreateNewQueueItem(newCFGRule, item.LastActionCoveredBeforeThisRule, LastActionCoveredByThisRule,
                    newCompletedStates, newPartiallyProcessedStates, plan, new List<int>((item as ICorrectionQueueItem).EndIndices).Append(LastActionCoveredByThisRule).ToList(), item.TotalMinNumberOfFlaws + this.MinNumberOfFlaws);

                    POQueueItem existingItem = null;

                    if (partiallyProcessedStatesByLastAction.Count > newQI.LastActionCoveredByThisRule)
                    {
                        partiallyProcessedStatesByLastAction[newQI.LastActionCoveredByThisRule].TryGetValue(newQI, out existingItem);
                    }
                    if (existingItem == null && completedStatesByFirstAction.Count > newQI.LastActionCoveredBeforeThisRule)
                    {
                        completedStatesByFirstAction[newQI.LastActionCoveredBeforeThisRule].TryGetValue(newQI, out existingItem);
                    }

                    Debug.Assert(existingItem != null, $"The existing item is null. The new queue item is {newQI}.");
                    existingItem.AddSubtaskCompletingRule(item.CFGRule.CurrentSubtaskIndex, this, queue);
                    (item as PredictionQueueItem).CompletedPredictionChildren.Add(existingItem);
                    return existingItem;
                }
                else
                {
                    return null;
                }
            }

            // Other methods:

            public void InitUniqueVersionID()
            {
                uniqueVersionID = ID;
            }
        }

    }
}
