using PlanRecognitionNETF;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using static PlanRecognitionExtension.PartialObservabilityEarleyParser.POQueueItem;

namespace PlanRecognitionExtension
{
    internal class PartialObservabilityEarleyParser : EarleyParser
    {
        public bool ANYTIME_GOALS { get; protected set; }
        public bool ALLOW_INSERTING_NEW_ACTIONS { get; protected set; }
        public bool ALLOW_DELETING_ACTIONS { get; protected set; }
        public bool RETURN_FIRST_SOLUTION { get; protected set; }
         

        protected HashSet<POQueueItem> foundCandidateGoalItemsReachingMaxDepthDuringLastGrounding = [];

        internal interface IHeuristic
        {
            double ComputeHeuristic(POQueueItem queueItem);
        }

        internal class MinFlawsIncludingUncoveredActionsHeuristic : IHeuristic
        {
            readonly int planLength;

            public MinFlawsIncludingUncoveredActionsHeuristic(int planLength)
            {
                this.planLength = planLength;
            }

            public double ComputeHeuristic(POQueueItem queueItem)
            {
                return queueItem.MinNumberOfFlaws + planLength + queueItem.LastActionCoveredBeforeThisRule - 1 - queueItem.LastActionCoveredByThisRule;
            }
        }

        internal class PriorityQueueWatchingFlaws 
        {
            class Priority : IComparable<Priority>
            {
                public Priority(double priorityValue, int time, POQueueItem queueItem)
                {
                    PriorityValue = priorityValue;
                    Time = time;
                    QueueItem = queueItem;
                }

                internal double PriorityValue { get; }
                internal int Time { get; }
                internal POQueueItem QueueItem { get; }

                public int CompareTo(Priority other)
                {
                    int result = PriorityValue.CompareTo(other.PriorityValue);

                    if (result == 0)
                    {
                        result = (other.QueueItem is CompleterQueueItem).CompareTo(QueueItem is CompleterQueueItem);
                    }

                    if (result == 0)
                    {
                        result = (other.QueueItem.LastActionCoveredByThisRule).CompareTo(QueueItem.LastActionCoveredByThisRule);
                    }

                    if (result == 0)
                    {
                        return Time.CompareTo(other.Time);
                    }
                    else
                    {
                        return result;
                    }
                }
            }
            readonly PriorityQueue<POQueueItem, Priority> queue = new();
            readonly SortedDictionary<int, int> minNumberOfFlawsOccurrences = new();
            public int Count { get; private set; }
            readonly HashSet<int> dequeuedIDs = new();
            readonly HashSet<int> enqueuedIDs = new();
            int time;

            static double GetRealItemPriority(POQueueItem item)
            {
                return item.TotalMinNumberOfFlaws;
            }

            internal double GetItemPriority(POQueueItem item)
            {
                return queue.UnorderedItems.Where(x => x.Element.ID == item.ID).First().Priority.PriorityValue;
            }

            internal void Decrease(POQueueItem queueItem, int previousMinFlaws)
            {
                queue.Remove(queueItem, out var _, out var _);
                Enqueue(queueItem);

#if DEBUG
                Console.WriteLine($"\tDECREASED: {queueItem}");
#endif

                

#if DEBUG
                
#endif
            }

#if DEBUG
            internal void checkEnqueued()
            {
                SortedDictionary<int, int> realMinNumberOfFlawsOccurrences = new();
                HashSet<int> seenIDs = new HashSet<int>();

                foreach (var kpp in queue.UnorderedItems)
                {
                    if (enqueuedIDs.Contains(kpp.Element.ID) && !seenIDs.Contains(kpp.Element.ID))
                    {
                        if (!realMinNumberOfFlawsOccurrences.ContainsKey(kpp.Element.TotalMinNumberOfFlaws))
                        {
                            realMinNumberOfFlawsOccurrences.Add(kpp.Element.TotalMinNumberOfFlaws, 0);
                        }
                        realMinNumberOfFlawsOccurrences[kpp.Element.TotalMinNumberOfFlaws]++;
                        seenIDs.Add(kpp.Element.ID);
                    }
                }

                Debug.Assert(enqueuedIDs.IsSubsetOf(seenIDs));

                foreach (var realFlaws in realMinNumberOfFlawsOccurrences)
                {
                    Debug.Assert(realFlaws.Value == minNumberOfFlawsOccurrences[realFlaws.Key]);
                }
                foreach (var storedFlaws in minNumberOfFlawsOccurrences)
                {
                    Debug.Assert(storedFlaws.Value == realMinNumberOfFlawsOccurrences[storedFlaws.Key]);
                }
            }
#endif

            internal void Enqueue(POQueueItem queueItem) 
            {
                double priority = GetRealItemPriority(queueItem);
                if (!enqueuedIDs.Contains(queueItem.ID))
                {
                    queue.Enqueue(queueItem, new Priority(priority, time++, queueItem));
                    enqueuedIDs.Add(queueItem.ID);
                    Count++;
                }
                else
                {
                    Debug.Assert(false, $"Item {queueItem} is already enqueued. Cannot enqueue again.");
                }
#if DEBUG
                
#endif

#if DEBUG
                
#endif
            }

            internal POQueueItem Dequeue()
            {
#if DEBUG
                
#endif
                var item = queue.Dequeue();
                enqueuedIDs.Remove(item.ID);
                Count--;

                
#if DEBUG
                
#endif
                return item;
            }

#if DEBUG
            internal void checkValues()
            {
                if (queue.Count > 0)
                {
                    double minVal = queue.Peek().TotalMinNumberOfFlaws;
                    for (int i = 0; i < minVal; i++)
                    {
                        if (minNumberOfFlawsOccurrences.ContainsKey(i))
                        {
                            Debugger.Break();
                        }
                    }
                }

                SortedDictionary<int, int> realMinNumberOfFlawsOccurrences = new();
                foreach (var (Key, _) in queue.UnorderedItems)
                {
                    if (!realMinNumberOfFlawsOccurrences.ContainsKey(Key.TotalMinNumberOfFlaws))
                    {
                        realMinNumberOfFlawsOccurrences.Add(Key.TotalMinNumberOfFlaws, 0);
                    }
                    realMinNumberOfFlawsOccurrences[Key.TotalMinNumberOfFlaws]++;
                }

                

                
            }
#endif

            internal int MinNumberOfFlawsInQueue()
            {
                if (enqueuedIDs.Count == 0)
                {
                    return int.MaxValue;
                }
                return queue.Peek().TotalMinNumberOfFlaws;
                
            }

            internal void EnqueueAgain(POQueueItem queueItem) 
            {
                double priority = GetRealItemPriority(queueItem);
                dequeuedIDs.Remove(queueItem.ID);
                Enqueue(queueItem);
            }

            internal bool IsEnqueued(POQueueItem existingItem)
            {
                return enqueuedIDs.Contains(existingItem.ID);
            }
        }

        internal virtual POQueueItem CreateQueueItemAndAddToTables(CFGRule cFGRule, int firstActionIndex, int lastActionIndex,
                List<HashSet<POQueueItem>> completedStatesByFirstAction,
                List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Action> plan, int numberOfFlaws = 0, bool coversActionInPrefix = false)
        {
            if (!cFGRule.TryGetNextTask(out CFGTask nextTask))
            {
                CompleterQueueItem result = new CompleterQueueItem(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws, coversActionInPrefix);
                AddNewStateToSetByFirstAction(completedStatesByFirstAction, result);
                return result;
            }

            else
            {
                POQueueItem result = null;

                if (nextTask is PrimitiveTask)
                {
                    result = new ScannerQueueItem(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws, plan, this);
                }

                else if (nextTask is AbstractTask)
                {
                    result = new PredictorQueueItem(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws);
                }

                else
                {
                    Debugger.Break();
                }

                if (result != null)
                {
                    AddNewStateToSetByLastAction(partiallyProcessedStatesByLastAction, result);
                }

                return result;
            }
        }

        protected static void AddNewStateToSetByLastAction(List<HashSet<POQueueItem>> setOfStates, POQueueItem queueItem)
        {
            AddNewStateToSet(setOfStates, queueItem, queueItem.LastActionCoveredByThisRule);
        }

        protected static void AddNewStateToSetByFirstAction(List<HashSet<POQueueItem>> setOfStates, POQueueItem queueItem)
        {
            AddNewStateToSet(setOfStates, queueItem, queueItem.LastActionCoveredBeforeThisRule);
        }

        static void AddNewStateToSet(List<HashSet<POQueueItem>> setOfStates, POQueueItem queueItem, int index)
        {
            while (setOfStates.Count <= index)
            {
                setOfStates.Add(new HashSet<POQueueItem>());
            }
            setOfStates[index].Add(queueItem);
        }

        internal abstract class POQueueItem 
        {
            public CFGRule CFGRule { get; }
            internal int LastActionCoveredBeforeThisRule { get; }
            internal int LastActionCoveredByThisRule { get; private set; }
            internal bool CoversActionInPrefix { get; } 
        
            public int MinNumberOfFlaws { get; set; }

            protected static int NumberOfInstances;
            internal int ID { get; private set; }
            internal Dictionary<POQueueItem, int>[] SubtaskCompletingRules { get; }  
            // with last version
            public int Version { get; protected set; }
            protected HashSet<Tuple<POQueueItem, int>> RulesCompletedByThisRule { get; } = new();
            public int[] MinNumberOfFlawsBySubtask { get; protected set; }
            public int SubtreeSize { get; private set; } = 1;
            public int MinNumberOfFlawsBeforeThisDecomposition { get; protected set; }



            internal virtual void DecreaseMinNumberOfFlawsBeforeThisDecomposition(int minFlaws, PriorityQueueWatchingFlaws queue)
            {
                if (minFlaws < MinNumberOfFlawsBeforeThisDecomposition)
                {
                    int previousFlaws = TotalMinNumberOfFlaws;
                    MinNumberOfFlawsBeforeThisDecomposition = minFlaws;
                    if (queue.IsEnqueued(this))
                    {
                        queue.Decrease(this, previousFlaws);
                    }
                }
                else
                {
                    Debug.Assert(false);
                }
            }

            internal void InitMinNumberOfFlawsBeforeThisDecomposition(int minFlaws)
            {
                if (MinNumberOfFlawsBeforeThisDecomposition == 0)
                {
                    MinNumberOfFlawsBeforeThisDecomposition = minFlaws;
                }
                else
                {
                    Debug.Assert(false);
                }
            }



            public int TotalMinNumberOfFlaws => MinNumberOfFlaws + MinNumberOfFlawsBeforeThisDecomposition;


            internal class QueueItemGroundingWrapper : IGroundable
            {
                internal int NextMinNumberOfFlaws { get; private set; }
                internal List<Action> CoveredActions { get; private set; }
                static int groundingDepth = 0;
                public QueueItemGroundingWrapper(POQueueItem queueItem)
                {

                    QueueItem = queueItem;
                    SubtasksWrappers = new SubtaskCompletingRuleGroundingWrapper[queueItem.SubtaskCompletingRules.Length];
                  
                }

                internal POQueueItem QueueItem { get; }
                internal SubtaskCompletingRuleGroundingWrapper[] SubtasksWrappers { get; }
                bool allSubtaskskInitialized = false;

                internal bool AllSubtasksFinished()
                {
                    foreach (var subtask in SubtasksWrappers) 
                    { 
                        if (!subtask.Finished)
                        {
                            return false;
                        }
                    }

                    return true;
                }

                internal void RecomputeNextMinNumberOfFlaws()
                {
                    if (allSubtaskskInitialized)
                    {
                        if (QueueItem.CFGRule.IsLeaf())
                        {
                            NextMinNumberOfFlaws = QueueItem.MinNumberOfFlaws;
                        }
                        else
                        {
                            NextMinNumberOfFlaws = 0;
                            foreach (var subtaskWrapper in SubtasksWrappers)
                            {
                                NextMinNumberOfFlaws += subtaskWrapper.NextMinNumberOfFlaws; 
                            }
                        }
                    }
                }

                public CFGRule GetCFGRule()
                {
                    return QueueItem.CFGRule;
                }

                public bool IsActionInPrefix(EarleyParser parser)
                {
                    return QueueItem.IsActionInPrefix(parser);
                }

                void InitializeSubtasks()
                {
                    for (int i = 0; i < QueueItem.SubtaskCompletingRules.Length; i++)
                    {
                        SubtasksWrappers[i] = new SubtaskCompletingRuleGroundingWrapper(i, this);
                        SubtasksWrappers[i].RecomputeNextMinNumberOfFlaws();
                    }
                    allSubtaskskInitialized = true;
                }

                public IEnumerable<Subplan> GetGroundedSubplans(EarleyParser parser,  
                    int currentCost, Slot lastSlot, double lastEndIndex, 
                    List<IGroundable> usedSubtaskCompletingRules, CancellationToken cancellationToken)
                {


                    InitializeSubtasks();
                    RecomputeNextMinNumberOfFlaws();
                    if (QueueItem.CFGRule.IsDummyRule(parser))
                    {
                        currentCost = parser.InputPlanPart.Count - QueueItem.LastActionCoveredByThisRule;
                    }

                    groundingDepth++;
#if DEBUG
                    Console.WriteLine($"***DEPTH: {groundingDepth}***");
#endif

                    if (groundingDepth > parser.MaxGroundingDepth)
                    {
#if DEBUG
                        Console.WriteLine("\tReached maximum grounding depth.");
#endif
                        groundingDepth--;
                        yield break;
                    }
                    else
                    {

                            foreach (var subplan in QueueItem.CFGRule.GetGroundedSubplans(
                                parser, this,
                                parser.CurrentPlan.Count,
                                SubtasksWrappers,
                                MaxAllowedCost(parser), currentCost,
                                lastSlot, lastEndIndex, usedSubtaskCompletingRules,
                                cancellationToken, groundingDepth))
                            {
#if DEBUG
                                Console.WriteLine($"\tReturned grounded subplan: {subplan}");
#endif
                                RecomputeNextMinNumberOfFlaws();

                                if (QueueItem.PreconditionsSatisfied(subplan, parser))
                                {
                                    groundingDepth--;


                                    yield return subplan;
                                    groundingDepth++;
                                }

                            }

                            groundingDepth--;
                        }
                    
                }

                public override string ToString()
                {
                    return QueueItem.ToString();
                }

                public int ComputeMinCost()
                {
                    if (GetCFGRule().IsEmptyRule)
                    {
                        return 0;
                    }

                    if (!allSubtaskskInitialized)
                    {
                        InitializeSubtasks();
                    }

                    int cost = 0;
                    foreach (var subtaskWrapper in SubtasksWrappers)
                    {
                        if (subtaskWrapper.SortedSubtaskCompletingRules.Count > 0 && subtaskWrapper.Range() == 0)
                        {
                            cost++;
                        }
                    }
                    if (QueueItem.LastActionCoveredBeforeThisRule == QueueItem.LastActionCoveredByThisRule)
                    {
                        cost--; // One flaw already added in the higher level.
                    }
                    return cost;
                }

                public int MaxAllowedCost(EarleyParser parser)
                {
                    return parser.CurrentMaxAllowedCost; 
                }

                public bool SetVariablesFromMainTask(CFGTask cFGTask)
                {
                    return QueueItem.CFGRule.SetVariablesFromMainTask(cFGTask);
                }

                public void ResetVariables()
                {
                    QueueItem.CFGRule.ResetVariables();
                }

                public int GetID()
                {
                    return QueueItem.ID;
                }


                public int GetIndexOfCoveredAction(int startIndex, EarleyParser parser)
                {
                    

                    return QueueItem.LastActionCoveredByThisRule - 1; 
                }
            }

            internal class SubtaskCompletingRuleGroundingWrapper : IEnumerable<IGroundable>
            {
                public SubtaskCompletingRuleGroundingWrapper(int subtaskIndex, QueueItemGroundingWrapper correspondingQueueItemWrapper)
                {

                    CorrespondingQueueItemWrapper = correspondingQueueItemWrapper; 
                    var sortedSubtaskCompletingRules = SortSubtaskCompletingRules(
                        correspondingQueueItemWrapper.QueueItem.SubtaskCompletingRules[subtaskIndex].Keys);
                    foreach (var rule in sortedSubtaskCompletingRules)
                    {

                        SortedSubtaskCompletingRules.Add(new(rule));
                    }
                }

                internal List<QueueItemGroundingWrapper> SortedSubtaskCompletingRules { get; } = new();
                internal int NextSubtaskCompletingRuleToBeUsedForGrounding { get; private set; }
                internal int NextMinNumberOfFlaws { get; private set; }
                internal QueueItemGroundingWrapper CorrespondingQueueItemWrapper { get; private set; }
                internal bool Finished { get; private set; }
                public IEnumerator<IGroundable> GetEnumerator()
                {
                    foreach (var item in SortedSubtaskCompletingRules)
                    {
#if DEBUG
                        //Console.WriteLine($"\t\tSCR {item} ");
                        //Console.WriteLine($"for item {this.CorrespondingQueueItemWrapper}");
#endif
                        int i = NextSubtaskCompletingRuleToBeUsedForGrounding;
                        NextSubtaskCompletingRuleToBeUsedForGrounding++;
                        Finished = false;

                        if (NextSubtaskCompletingRuleToBeUsedForGrounding == SortedSubtaskCompletingRules.Count)
                        {
                            NextSubtaskCompletingRuleToBeUsedForGrounding = 0;
                            Finished = true;
                        }

                        RecomputeNextMinNumberOfFlaws();



                        yield return item;
                    }
                }

                internal int Range()
                {
                    if (SortedSubtaskCompletingRules.Count > 0)
                    {
                        var bestRule = SortedSubtaskCompletingRules.First().QueueItem;
                        if (bestRule.CFGRule.IsEmptyRule)
                        {
                            return 1; // "dummy 1" - does not increase cost
                        }
                        else
                        {
                            return SortedSubtaskCompletingRules.First().QueueItem.LastActionCoveredByThisRule -
                        SortedSubtaskCompletingRules.First().QueueItem.LastActionCoveredBeforeThisRule;
                        }
                    }
                    else
                    {
                        return 0;
                    }

                }

                internal void RecomputeNextMinNumberOfFlaws()
                {
                    int lastMinNumberOfFlaws = NextMinNumberOfFlaws;
                    if (SortedSubtaskCompletingRules.Count > 0)
                    {
                        if (SortedSubtaskCompletingRules.Count > NextSubtaskCompletingRuleToBeUsedForGrounding)
                        {
                            NextMinNumberOfFlaws = SortedSubtaskCompletingRules.Skip(NextSubtaskCompletingRuleToBeUsedForGrounding).Min(
                                x => x.NextMinNumberOfFlaws);
                        }
                        else
                        {
                            NextMinNumberOfFlaws = int.MaxValue;
                        }
                    }
                    if (NextMinNumberOfFlaws < lastMinNumberOfFlaws)
                    {
                        NextMinNumberOfFlaws = int.MaxValue;
                    }
                    CorrespondingQueueItemWrapper.RecomputeNextMinNumberOfFlaws();
                }

                IEnumerator IEnumerable.GetEnumerator()
                {
                    return GetEnumerator();
                }
            }

            public override string ToString()
            {
                return $"[ID: {ID}] [{CFGRule}] [({LastActionCoveredBeforeThisRule}, {LastActionCoveredByThisRule}]] [#flaws >= {TotalMinNumberOfFlaws}]";
            }

            protected POQueueItem(CFGRule cFGRule, int firstActionIndex, int lastActionIndex, int numberOfFlaws, bool coversActionInPrefix = false)
            {
                ID = NumberOfInstances++;
                CFGRule = cFGRule;
                LastActionCoveredBeforeThisRule = firstActionIndex;
                LastActionCoveredByThisRule = lastActionIndex;
                MinNumberOfFlaws = numberOfFlaws;
                CoversActionInPrefix = coversActionInPrefix;

                SubtaskCompletingRules = new Dictionary<POQueueItem, int>[CFGRule.Subtasks.Length];
                MinNumberOfFlawsBySubtask = new int[CFGRule.Subtasks.Length];
                for (int i = 0; i < CFGRule.Subtasks.Length; i++)
                {
                    SubtaskCompletingRules[i] = new();
                    MinNumberOfFlawsBySubtask[i] = 0;
                }
            }


            internal class QueueItemGroundingEnumerator : IEnumerator<Subplan>
            {
                static int numberOfInstances;
                internal int ID { get; }
                readonly EarleyParser parser;
                private readonly List<IGroundable> usedSubtaskCompletingRules;
                readonly CancellationToken cancellationToken;
                IEnumerator<Subplan> groundingEnumerator;
                readonly QueueItemGroundingWrapper queueItemWrapper;
                internal POQueueItem Root { get; }

                public Subplan Current => groundingEnumerator.Current;

                object IEnumerator.Current => Current;

                internal bool EnumerationFinished()
                {
                    return queueItemWrapper.AllSubtasksFinished();
                }

                public QueueItemGroundingEnumerator(QueueItemGroundingWrapper queueItemWrapper, EarleyParser parser,
List<IGroundable> usedSubtaskCompletingRules, CancellationToken cancellationToken)
                {
                    groundingEnumerator = queueItemWrapper.GetGroundedSubplans(parser, 
                        0, parser.DummyInitSlot, 0, usedSubtaskCompletingRules,
                        cancellationToken).GetEnumerator();
                    this.queueItemWrapper = queueItemWrapper;
                    Root = queueItemWrapper.QueueItem; 
                    this.parser = parser;
                    this.usedSubtaskCompletingRules = usedSubtaskCompletingRules;
                    this.cancellationToken = cancellationToken;
                    ID = numberOfInstances++;
                }

                public override string ToString()
                {
                    return queueItemWrapper.ToString();
                }

                internal int NextMinNumberOfFlaws()
                {
                    return queueItemWrapper.NextMinNumberOfFlaws;
                }

                public bool MoveNext()
                {
                    var result = groundingEnumerator.MoveNext();
                    queueItemWrapper.RecomputeNextMinNumberOfFlaws();
                    return result;
                }

                public void Reset()
                {
                    groundingEnumerator = queueItemWrapper.GetGroundedSubplans(parser, 0, 
                        parser.DummyInitSlot, 0, usedSubtaskCompletingRules,
                        cancellationToken).GetEnumerator();
                }

                public void Dispose()
                {
                    groundingEnumerator.Dispose();
                }
            }

            internal bool IsPossibleGoal(int totalPlanLength, out int goalMinNumberOfFlaws, AbstractTask dummyStartingTask, PartialObservabilityEarleyParser parser)
            {
                bool isGoal = CFGRule.MainTask.Equals(dummyStartingTask) && LastActionCoveredBeforeThisRule == 0 && CFGRule.Finished(); 
                if (!parser.ALLOW_DELETING_ACTIONS)
                {
                    isGoal = isGoal && LastActionCoveredByThisRule == totalPlanLength;
                }
                goalMinNumberOfFlaws = isGoal ? GoalMinNumberOflaws(totalPlanLength) : int.MaxValue;

                return isGoal;
            }

            internal int GoalMinNumberOflaws(int totalPlanLength)
            {
                return MinNumberOfFlaws + totalPlanLength - LastActionCoveredByThisRule;
            }

            internal bool CopySubtaskCompletingRulesFrom(POQueueItem otherItem, PriorityQueueWatchingFlaws queue)
            {
                bool changed = false;
                int prevFlaws = MinNumberOfFlaws;
                for (int i = 0; i < SubtaskCompletingRules.Length; i++)
                {
                    foreach (var rule in otherItem.SubtaskCompletingRules[i])
                    {
                        if (!SubtaskCompletingRules[i].TryGetValue(rule.Key, out int version) || version != rule.Value)
                        {
                            changed = true;
                            SubtaskCompletingRules[i][rule.Key] = rule.Value;
                            rule.Key.RulesCompletedByThisRule.Add(new(this, i));
                            if (SubtaskCompletingRules[i].Count == 1)
                            {
                                MinNumberOfFlawsBySubtask[i] = otherItem.MinNumberOfFlawsBySubtask[i];  
                            }
                        }
                        else
                        { }
                        MinNumberOfFlawsBySubtask[i] = Math.Min(MinNumberOfFlawsBySubtask[i], otherItem.MinNumberOfFlawsBySubtask[i]);
                    }
                }
                MinNumberOfFlaws = MinNumberOfFlawsBySubtask.Sum();
                if (prevFlaws > MinNumberOfFlaws)
                {
                    UpdateDependingRulesMinFlaws(queue);
                }



                if (!changed)
                {
                    changed = CheckTreeSize();
                }


                if (changed)
                {
                    ChangeVersion();
                }



                return changed;
            }

            internal virtual bool AddSubtaskCompletingRule(int subtaskIndex, POQueueItem completingRule, PriorityQueueWatchingFlaws queue) 
            {
                bool changed = false;
                int prevMinFlaws = MinNumberOfFlaws;
                int previousNumberOfCompletingRules = SubtaskCompletingRules[subtaskIndex].Count;

                if (!SubtaskCompletingRules[subtaskIndex].TryGetValue(completingRule, out int version) ||
                    version != completingRule.Version)
                {
                    SubtaskCompletingRules[subtaskIndex][completingRule] = completingRule.Version;
                    changed = true;
                }

                if (completingRule.MinNumberOfFlaws < MinNumberOfFlawsBySubtask[subtaskIndex])
                {
                    MinNumberOfFlaws -= MinNumberOfFlawsBySubtask[subtaskIndex];
                    MinNumberOfFlawsBySubtask[subtaskIndex] = completingRule.MinNumberOfFlaws;
                    MinNumberOfFlaws += completingRule.MinNumberOfFlaws;
                }
                else if (previousNumberOfCompletingRules == 0)
                {
                    MinNumberOfFlawsBySubtask[subtaskIndex] = completingRule.MinNumberOfFlaws;
                    MinNumberOfFlaws += completingRule.MinNumberOfFlaws;
                }

                bool added = false;

                if (previousNumberOfCompletingRules < SubtaskCompletingRules[subtaskIndex].Count)
                {
                    added = true;
                }

                if (prevMinFlaws != MinNumberOfFlaws)
                {
                    UpdateDependingRulesMinFlaws(queue);
                }

                
                changed = CheckTreeSize();


                if (changed)
                {
                    ChangeVersion();
                }

                return changed;
            }

            internal void ChangeVersion()
            {
                ChangeVersion(new HashSet<int>() { ID });
            }

            void ChangeVersion(HashSet<int> visitedQIIDs)
            {
                Version++;

                foreach (var r in RulesCompletedByThisRule)
                {
                    if (!visitedQIIDs.Contains(r.Item1.ID))
                    {
                        visitedQIIDs.Add(r.Item1.ID);
                        r.Item1.ChangeVersion(visitedQIIDs);
                    }
                }
            }

            internal bool CheckTreeSize()
            {
                int lastSize = SubtreeSize;
                SubtreeSize = 1;
                CheckTreeSize(this, new HashSet<int> { this.ID });
                return SubtreeSize > lastSize;
            }

            private void CheckTreeSize(POQueueItem currentNode, HashSet<int> queueItemsOnPath)
            {
                foreach (var subtasks in currentNode.SubtaskCompletingRules)
                {
                    foreach (var subtask in subtasks)
                    {
                        if (!queueItemsOnPath.Contains(subtask.Key.ID))
                        {
                            SubtreeSize++;
                            queueItemsOnPath.Add(subtask.Key.ID);
                            CheckTreeSize(subtask.Key, queueItemsOnPath);
                            queueItemsOnPath.Remove(subtask.Key.ID);
                        }
                    }
                }
            }

            void UpdateDependingRulesMinFlaws(PriorityQueueWatchingFlaws queue)
            {
                foreach (var rule in RulesCompletedByThisRule)
                {
                    int prevMinFlaws = rule.Item1.TotalMinNumberOfFlaws;
                    if (prevMinFlaws > rule.Item1.TotalMinNumberOfFlaws && queue.IsEnqueued(rule.Item1))
                    {
                        queue.Decrease(rule.Item1, prevMinFlaws);
                    }
                }
            }

            internal abstract void Process(PriorityQueueWatchingFlaws queue, List<Action> plan, HashSet<Action> allEmptyActions,
                List<HashSet<POQueueItem>> completedStatesByFirstAction, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction,
                List<Rule> allRules, List<HashSet<POQueueItem>> cFGRulesGeneratedByPredictorByStartingIndex, PartialObservabilityEarleyParser parser);

            

            protected static void AddStatesToSetByLastAction(List<HashSet<POQueueItem>> source, List<HashSet<POQueueItem>> destination)
            {
                foreach (var hs in  source)
                {
                    foreach (var item in hs)
                    {
                        AddNewStateToSetByLastAction(destination, item);
                    }
                }
            }

            protected static void AddStatesToSetByFirstAction(List<HashSet<POQueueItem>> source, List<HashSet<POQueueItem>> destination)
            {
                foreach (var hs in source)
                {
                    foreach (var item in hs)
                    {
                        AddNewStateToSetByFirstAction(destination, item);
                    }
                }
            }

            

            public override bool Equals(object obj)
            {
                return obj is POQueueItem item &&
                        LastActionCoveredBeforeThisRule == item.LastActionCoveredBeforeThisRule &&
                        LastActionCoveredByThisRule == item.LastActionCoveredByThisRule &&
                        CFGRule.Equals(item.CFGRule);
            }

            class NullWriter : TextWriter
            {
                public override Encoding Encoding => Encoding.UTF8;
            }

            internal bool PreconditionsSatisfied(Subplan subplan, EarleyParser parser)
            {
                return true; // for increased efficiency ... not finished yet

                List<TaskType> allTaskTypes = parser.AllTaskTypes;
                List<ActionType> allActionTypes = parser.AllActionTypes;
                List<Constant> allConstants = parser.AllConstants;


                if (LastActionCoveredBeforeThisRule > 0) // not applicable yet
                {
                    return true;
                }

                List<Term> plan = new List<Term>();
                foreach (var slot in subplan.Timeline)
                {
                    if (slot.a != null)
                    {
                        plan.Add(slot.a);
                    }
                }

                List<Subplan> planPrefix = parser.InputPlanPart;
                List<Slot> prefixTimeline = parser.PrefixTimeline;
                var consoleOutput = Console.Out;
                Console.SetOut(new NullWriter());
                bool planOK = parser.Init(plan, allTaskTypes, allActionTypes, allConstants, parser.InitialState);
                parser.InputPlanPart = planPrefix;
                parser.PrefixTimeline = prefixTimeline;
                Console.SetOut(consoleOutput);

             

                return planOK;
            }

 

            internal static List<POQueueItem> SortSubtaskCompletingRules(IEnumerable<POQueueItem> subtaskCompletingRules)
            {
                List<POQueueItem> result = subtaskCompletingRules.ToList();
                result.Sort((x, y) => x.MinNumberOfFlaws.CompareTo(y.MinNumberOfFlaws));
                return result;
            }

            public CFGRule GetCFGRule()
            {
                return CFGRule;
            }

            public bool IsActionInPrefix(EarleyParser parser)
            {
                return CoversActionInPrefix;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    int cFGRuleHash = CFGRule.GetHashCode();
                    hash = hash * 31 + cFGRuleHash;
                    hash = hash * 31 + LastActionCoveredBeforeThisRule;
                    hash = hash * 31 + LastActionCoveredByThisRule;
                    return hash;
                }
            }
        }

        internal abstract class PredictionQueueItem : POQueueItem
        {
            internal HashSet<POQueueItem> CompletedPredictionChildren { get; } = new();
            internal HashSet<POQueueItem> PredictionChildren { get; } = new();

            internal override void DecreaseMinNumberOfFlawsBeforeThisDecomposition(int minFlaws, PriorityQueueWatchingFlaws queue)
            {
                if (minFlaws < MinNumberOfFlawsBeforeThisDecomposition)
                {
                    base.DecreaseMinNumberOfFlawsBeforeThisDecomposition(minFlaws, queue);
                    RecomputeChildrenMinFlawsBeforeDecomposition(queue);
                }
                else
                {
                    Debug.Assert(false);
                }
            }

            internal override bool AddSubtaskCompletingRule(int subtaskIndex, POQueueItem completingRule, PriorityQueueWatchingFlaws queue)
            {
                int previousTotalFlaws = TotalMinNumberOfFlaws;
                bool result = base.AddSubtaskCompletingRule(subtaskIndex, completingRule, queue);
                if (previousTotalFlaws > TotalMinNumberOfFlaws)
                {
                    RecomputeChildrenMinFlawsBeforeDecomposition(queue);
                }
                return result;
            }

            protected PredictionQueueItem(CFGRule cFGRule, int firstActionIndex, int lastActionIndex, int numberOfFlaws, bool coversActionInPrefix = false) : base(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws, coversActionInPrefix)
            {
            }

            protected void RecomputeChildrenMinFlawsBeforeDecomposition(PriorityQueueWatchingFlaws queue)
            {
                foreach (var child in PredictionChildren)
                {
                    if (child.MinNumberOfFlawsBeforeThisDecomposition > TotalMinNumberOfFlaws)
                    {
                        child.DecreaseMinNumberOfFlawsBeforeThisDecomposition(TotalMinNumberOfFlaws, queue);
                    }
                }

                foreach (var child in CompletedPredictionChildren)
                {
                    if (child.MinNumberOfFlawsBeforeThisDecomposition > MinNumberOfFlawsBeforeThisDecomposition)
                    {
                        child.DecreaseMinNumberOfFlawsBeforeThisDecomposition(MinNumberOfFlawsBeforeThisDecomposition, queue);
                    }
                }
            }
        }

        internal class CompleterQueueItem : POQueueItem
        {
            internal HashSet<POQueueItem> PredictorItemsCompletedByThisRule { get; } = new();

            internal CompleterQueueItem(CFGRule cFGRule, int firstActionIndex, int lastActionIndex, int numberOfFlaws, bool coversActionInPrefix = false)
                : base(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws, coversActionInPrefix)
            {
            }

            internal POQueueItem ProcessPredictorItem(POQueueItem item, List<HashSet<POQueueItem>> newCompletedStates,
                PriorityQueueWatchingFlaws queue, List<Action> plan, HashSet<Action> allEmptyActions,
                List<HashSet<POQueueItem>> completedStatesByFirstAction,
                List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction,
                List<Rule> allRules, List<HashSet<POQueueItem>> cFGRulesGeneratedByPredictorByStartingIndex,
                PartialObservabilityEarleyParser parser)
            {
                
                while (newCompletedStates.Count <= LastActionCoveredBeforeThisRule)
                {
                    newCompletedStates.Add([]);
                }
                newCompletedStates[LastActionCoveredBeforeThisRule].Add(this);

                List<HashSet<POQueueItem>> newPartiallyProcessedStates = new();
                
                var result = ProcessPredictorItem(item, newPartiallyProcessedStates, newCompletedStates,
                    queue, plan, allEmptyActions, completedStatesByFirstAction, partiallyProcessedStatesByLastAction,
                    allRules, cFGRulesGeneratedByPredictorByStartingIndex, parser);
                AddStatesToSetByLastAction(newPartiallyProcessedStates, partiallyProcessedStatesByLastAction);
                AddStatesToSetByFirstAction(newCompletedStates, completedStatesByFirstAction);

                return result;
            }

            internal virtual POQueueItem ProcessPredictorItem(POQueueItem item,
                List<HashSet<POQueueItem>> newPartiallyProcessedStates, List<HashSet<POQueueItem>> newCompletedStates,
                PriorityQueueWatchingFlaws queue, List<Action> plan, HashSet<Action> allEmptyActions,
                List<HashSet<POQueueItem>> completedStatesByFirstAction,
                List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction,
                List<Rule> allRules, List<HashSet<POQueueItem>> cFGRulesGeneratedByPredictorByStartingIndex,
                PartialObservabilityEarleyParser parser, bool onlyNewItemDontUpdateExisting = false)
            {
                PredictorItemsCompletedByThisRule.Add(item);
                CFGRule newCFGRule = CloneAndFillVarsBySubtaskInstantiation(item.CFGRule, CFGRule.MainTask.Task.TaskInstance,
                item.CFGRule.CurrentSubtaskIndex, parser);
                newCFGRule.IncrementCurrentSubtaskIndex();
                POQueueItem newQI = parser.CreateQueueItemAndAddToTables(newCFGRule, item.LastActionCoveredBeforeThisRule, LastActionCoveredByThisRule,
                    newCompletedStates, newPartiallyProcessedStates, plan);
                newQI.InitMinNumberOfFlawsBeforeThisDecomposition(item.MinNumberOfFlawsBeforeThisDecomposition);
                POQueueItem existingItem = null;
                if (partiallyProcessedStatesByLastAction.Count > newQI.LastActionCoveredByThisRule)
                {
                    partiallyProcessedStatesByLastAction[newQI.LastActionCoveredByThisRule].TryGetValue(newQI, out existingItem);
                }
                if (existingItem == null && completedStatesByFirstAction.Count > newQI.LastActionCoveredBeforeThisRule)
                {
                    completedStatesByFirstAction[newQI.LastActionCoveredBeforeThisRule].TryGetValue(newQI, out existingItem);
                }

                if (existingItem != null)
                {
                    int previousMinFlaws = existingItem.TotalMinNumberOfFlaws;
                    bool existingRuleAlreadyCompletedByThisItem = !RulesCompletedByThisRule.Add(new(existingItem, item.CFGRule.CurrentSubtaskIndex));
                    bool changed = existingItem.CopySubtaskCompletingRulesFrom(item, queue);
                    changed |= existingItem.AddSubtaskCompletingRule(item.CFGRule.CurrentSubtaskIndex, this, queue);
                    (item as PredictionQueueItem).CompletedPredictionChildren.Add(existingItem);
                    bool alreadyDecreased = false;

                    if (item.MinNumberOfFlawsBeforeThisDecomposition < existingItem.MinNumberOfFlawsBeforeThisDecomposition)
                    { 
                        existingItem.DecreaseMinNumberOfFlawsBeforeThisDecomposition(item.MinNumberOfFlawsBeforeThisDecomposition, queue);
                        if (queue.IsEnqueued(existingItem))
                        {
                            alreadyDecreased = true;
                        }
                    }

                    if (existingItem is PredictorQueueItem predictorQueueItem)
                    {
                        existingItem.CFGRule.TryGetNextTask(out CFGTask nextTask);
                        TaskType desiredTaskType = (nextTask as AbstractTask).Task.TaskType;

                        if (completedStatesByFirstAction.Count > LastActionCoveredByThisRule)
                        {
                            List<HashSet<POQueueItem>> newCompletedStatesByLastActionBefore = new();
                            foreach (var completedRule in completedStatesByFirstAction[LastActionCoveredByThisRule])
                            {
                                if (CFGTask.SameTypeTasks(completedRule.CFGRule.MainTask, nextTask) &&
                                    NonConflictingInstantiations(completedRule.CFGRule.MainTask.GetConstants(),
                                    nextTask.GetConstants()))
                                {
                                    predictorQueueItem.PredictionChildren.Add(completedRule);
                                    if (TotalMinNumberOfFlaws < completedRule.MinNumberOfFlawsBeforeThisDecomposition)
                                    {
                                        completedRule.DecreaseMinNumberOfFlawsBeforeThisDecomposition(TotalMinNumberOfFlaws, queue);
                                    }

                                    List<HashSet<POQueueItem>> newCompletedStatesByFirstAction = new();
                                    for (int i = 0; i < completedStatesByFirstAction.Count; i++)
                                    {
                                        newCompletedStatesByFirstAction.Add(new(
                                            completedStatesByFirstAction[i]));
                                    }
                                    (completedRule as CompleterQueueItem).ProcessPredictorItem(existingItem, newCompletedStatesByLastActionBefore,
                                        queue, plan, allEmptyActions, newCompletedStatesByFirstAction,
                                        partiallyProcessedStatesByLastAction, allRules, cFGRulesGeneratedByPredictorByStartingIndex,
                                        parser);
                                    completedStatesByFirstAction.Clear();
                                    for (int i = 0; i < newCompletedStatesByFirstAction.Count; i++)
                                    {
                                        completedStatesByFirstAction.Add(new(
                                            newCompletedStatesByFirstAction[i]));
                                    }

                                }
                            }
                            AddStatesToSetByFirstAction(newCompletedStatesByLastActionBefore,
                                completedStatesByFirstAction);
                        }
                    }

                    if (changed && !queue.IsEnqueued(existingItem) && !existingRuleAlreadyCompletedByThisItem)
                    {
                        queue.Enqueue(existingItem);

                    }
                    else if (previousMinFlaws > existingItem.TotalMinNumberOfFlaws && queue.IsEnqueued(existingItem) && !alreadyDecreased)
                    {
                        queue.Decrease(existingItem, previousMinFlaws);
                    }

                }
                else
                {
                    newQI.CopySubtaskCompletingRulesFrom(item, queue);
                    RulesCompletedByThisRule.Add(new(newQI, item.CFGRule.CurrentSubtaskIndex));
 
                    newQI.AddSubtaskCompletingRule(item.CFGRule.CurrentSubtaskIndex, this, queue);
                    (item as PredictionQueueItem).CompletedPredictionChildren.Add(newQI);
                    queue.Enqueue(newQI);

                }

                return existingItem ?? newQI;
            }
    

            internal override void Process(PriorityQueueWatchingFlaws queue, List<Action> plan, HashSet<Action> allEmptyActions,
                List<HashSet<POQueueItem>> completedStatesByFirstAction, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction,
                List<Rule> allRules, List<HashSet<POQueueItem>> cFGRulesGeneratedByPredictorByStartingIndex, PartialObservabilityEarleyParser parser)
            {

                while (completedStatesByFirstAction.Count <= LastActionCoveredBeforeThisRule)
                {
                    completedStatesByFirstAction.Add(new HashSet<POQueueItem>());
                }
                completedStatesByFirstAction[LastActionCoveredBeforeThisRule].Add(this);

                List<HashSet<POQueueItem>> newPartiallyProcessedStates = new();
                List<HashSet<POQueueItem>> newCompletedStates = new();


                foreach (var item in partiallyProcessedStatesByLastAction[LastActionCoveredBeforeThisRule])
                {
                    if (item.CFGRule.TryGetNextTask(out CFGTask nextTask) && CFGTask.SameTypeTasks(nextTask, CFGRule.MainTask) &&
                        NonConflictingInstantiations(nextTask.GetConstants(), CFGRule.MainTask.GetConstants()))
                    {
                        List<HashSet<POQueueItem>> newPartiallyProcessedStatesByLastAction = new();
                        for (int i = 0; i < partiallyProcessedStatesByLastAction.Count; i++)
                        {
                            newPartiallyProcessedStatesByLastAction.Add(new
                                HashSet<POQueueItem>(partiallyProcessedStatesByLastAction[i]));
                        }

                        ProcessPredictorItem(item, newPartiallyProcessedStates, newCompletedStates,
                        queue, plan, allEmptyActions, completedStatesByFirstAction, newPartiallyProcessedStatesByLastAction,
                        allRules, cFGRulesGeneratedByPredictorByStartingIndex, parser);

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
            }
        }

        internal class PredictorQueueItem : PredictionQueueItem
        {

            internal PredictorQueueItem(CFGRule cFGRule, int firstActionIndex, int lastActionIndex, int numberOfFlaws) : base(cFGRule,
                firstActionIndex, lastActionIndex, numberOfFlaws)
            {
            }

            internal override void Process(PriorityQueueWatchingFlaws queue, List<Action> plan, HashSet<Action> allEmptyActions,
                List<HashSet<POQueueItem>> completedStatesByLastActionBefore, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction,
                List<Rule> allRules, List<HashSet<POQueueItem>> cFGRulesGeneratedByPredictorByStartingIndex, PartialObservabilityEarleyParser parser)
            {
                CFGRule.TryGetNextTask(out CFGTask nextTask);
                TaskType desiredTaskType = (nextTask as AbstractTask).Task.TaskType;


                if (completedStatesByLastActionBefore.Count > LastActionCoveredByThisRule)
                {
                    List<HashSet<POQueueItem>> newCompletedStatesByLastActionBefore = new();
                    var set = new HashSet<POQueueItem>(completedStatesByLastActionBefore[LastActionCoveredByThisRule]);
                    
                    foreach (var completedRule in set)
                    {
                        if (CFGTask.SameTypeTasks(completedRule.CFGRule.MainTask, nextTask) &&
                            NonConflictingInstantiations(completedRule.CFGRule.MainTask.GetConstants(), nextTask.GetConstants()))
                        {
                            PredictionChildren.Add(completedRule);
                            if (TotalMinNumberOfFlaws < completedRule.MinNumberOfFlawsBeforeThisDecomposition)
                            {
                                completedRule.DecreaseMinNumberOfFlawsBeforeThisDecomposition(TotalMinNumberOfFlaws, queue);
                            }
                            (completedRule as CompleterQueueItem).ProcessPredictorItem(this, newCompletedStatesByLastActionBefore,
                                queue, plan, allEmptyActions, completedStatesByLastActionBefore,
                                partiallyProcessedStatesByLastAction, allRules, cFGRulesGeneratedByPredictorByStartingIndex,
                                parser);

                        }
                    }
                    AddStatesToSetByFirstAction(newCompletedStatesByLastActionBefore, 
                        completedStatesByLastActionBefore);
                }

                foreach (var rule in allRules)
                {
                    if (rule.MainTaskType.Equals(desiredTaskType))
                    {
                        CFGTask[] subtasks = GetSubtasksForRule(nextTask as AbstractTask, rule, CFGRule);
                        CFGRule cFGRule = new CFGRule(nextTask.Clone() as AbstractTask, subtasks, rule, parser);

                        POQueueItem queueItem = parser.CreateQueueItemAndAddToTables(cFGRule, LastActionCoveredByThisRule, LastActionCoveredByThisRule, 
                            completedStatesByLastActionBefore, partiallyProcessedStatesByLastAction, plan);
                        queueItem.InitMinNumberOfFlawsBeforeThisDecomposition(TotalMinNumberOfFlaws);

                        if (cFGRulesGeneratedByPredictorByStartingIndex.Count > LastActionCoveredByThisRule && 
                            cFGRulesGeneratedByPredictorByStartingIndex[LastActionCoveredByThisRule].TryGetValue(queueItem, out var actualQueueItem))
                        {
                            if (TotalMinNumberOfFlaws < actualQueueItem.MinNumberOfFlawsBeforeThisDecomposition)
                            {
                                actualQueueItem.DecreaseMinNumberOfFlawsBeforeThisDecomposition(TotalMinNumberOfFlaws, queue);
                            }
                            continue;         
                        }

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

        internal class ScannerQueueItem : PredictionQueueItem
        {
            readonly PrimitiveTask nextActionToProcess;
            readonly int NumberOfFlawsBeforeScanning;
            protected readonly int LastActionCoveredBeforeScanning;
            internal int SkippedActions { get; private set; }
            internal PlanCorrectionOperation NextCorrection { get; private set; } = PlanCorrectionOperation.FindingActionInPlan;
            protected Action nextSuitableActionInPlan;
            protected PartialObservabilityEarleyParser parser;

            internal ScannerQueueItem(CFGRule cFGRule, int firstActionIndex, int lastActionIndex, int numberOfFlaws, List<Action> plan, PartialObservabilityEarleyParser parser) :
                base(cFGRule, firstActionIndex, lastActionIndex, numberOfFlaws)
            {
                this.parser = parser;
                CFGRule.TryGetNextTask(out CFGTask nextTask);
                nextActionToProcess = nextTask as PrimitiveTask;
                NumberOfFlawsBeforeScanning = numberOfFlaws;
                LastActionCoveredBeforeScanning = lastActionIndex;
                SetFirstOperation(plan);
            }

            void SetFirstOperation(List<Action> plan)
            {
                if (parser.FixedPrefix(NextIndexInPlanToTry(parser)))
                {
                    if (!TrySwitchToFindingActionInPlan(plan))
                    {
                        NextCorrection = PlanCorrectionOperation.Finished;
                    }
                }
                else
                {
                    if (parser.ALLOW_INSERTING_NEW_ACTIONS)
                    {
                        NextCorrection = PlanCorrectionOperation.AddingNewAction;
                        TrySwitchToFindingActionInPlan(plan);
                    }
                    else
                    {
                        NextCorrection = PlanCorrectionOperation.FindingActionInPlan;
                        SetNextActionInPlanIfSuitable(plan);
                    }
                }
            }

            internal override void Process(PriorityQueueWatchingFlaws queue, List<Action> plan, HashSet<Action> allEmptyActions,
                List<HashSet<POQueueItem>> completedStatesByFirstAction, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction,
                List<Rule> allRules, List<HashSet<POQueueItem>> cFGRulesGeneratedByPredictorByStartingIndex, PartialObservabilityEarleyParser parser)
            {
                
                if (parser.FixedPrefix(NextIndexInPlanToTry(parser)))
                {
                    if (NextCorrection !=
                    PlanCorrectionOperation.Finished)
                    {
                        IdentifyActionWithNextPlanAction(queue, completedStatesByFirstAction, 
                            partiallyProcessedStatesByLastAction, plan);
                    }
                }
                else
                {
                    if (parser.ALLOW_INSERTING_NEW_ACTIONS)
                    {
                        switch (NextCorrection)
                        {
                            case PlanCorrectionOperation.FindingActionInPlan:
                                IdentifyActionWithNextPlanAction(queue, completedStatesByFirstAction, partiallyProcessedStatesByLastAction, plan);
                                break;

                            case PlanCorrectionOperation.AddingNewAction:
                                InsertNewAction(queue, allEmptyActions, completedStatesByFirstAction, partiallyProcessedStatesByLastAction, plan);
                                break;

                            default:
                                throw new InvalidOperationException();
                        }

                        if (parser.ALLOW_DELETING_ACTIONS || NextCorrection == PlanCorrectionOperation.FindingActionInPlan)
                        {
                            SetNextScannerState(plan);
                            queue.Enqueue(this);
                        }
                    }
                    else if (parser.ALLOW_DELETING_ACTIONS && nextSuitableActionInPlan != null)
                    {
                        IdentifyActionWithNextPlanAction(queue, completedStatesByFirstAction, partiallyProcessedStatesByLastAction, plan);
                        SetNextScannerState(plan);
                        if (nextSuitableActionInPlan != null)
                        {

                            queue.Enqueue(this);
                        }
                    }
                }
            }

            void SetNextScannerState(List<Action> plan)
            {
                SetNextOperation(plan);
                SetNextNumberOfFlaws();
            }

            void SetNextNumberOfFlaws()
            {
                switch (NextCorrection)
                {
                    case PlanCorrectionOperation.FindingActionInPlan:
                        MinNumberOfFlaws = NumberOfFlawsBeforeScanning + SkippedActions;
                        break;
                    case PlanCorrectionOperation.AddingNewAction:
                        MinNumberOfFlaws = NumberOfFlawsBeforeScanning + SkippedActions + 1;
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            protected void SkipAction()
            {
                SkippedActions++;
            }

            protected int NextIndexInPlanToTry(PartialObservabilityEarleyParser parser)
            {
                if (parser.ALLOW_INSERTING_NEW_ACTIONS)
                {
                    switch (NextCorrection)
                    {
                        case PlanCorrectionOperation.AddingNewAction:
                            return LastActionCoveredBeforeScanning + SkippedActions; // plan is indexed from 0
                        default:
                            return LastActionCoveredBeforeScanning + SkippedActions;
                            
                    }
                }
                else
                {
                    return LastActionCoveredBeforeScanning + SkippedActions;
                }
            }

            

            void SetNextOperation(List<Action> plan)
            {
                if (parser.ALLOW_INSERTING_NEW_ACTIONS && parser.ALLOW_DELETING_ACTIONS)
                {
                    switch (NextCorrection)
                    {
                        case PlanCorrectionOperation.FindingActionInPlan:
                            NextCorrection = PlanCorrectionOperation.AddingNewAction;
                            break;
                        case PlanCorrectionOperation.AddingNewAction:
                            SkipAction();
                            TrySwitchToFindingActionInPlan(plan);
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
                else if (parser.ALLOW_DELETING_ACTIONS)
                {
                    SkipAction();
                    SetNextActionInPlanIfSuitable(plan);
                }
                else if (parser.ALLOW_INSERTING_NEW_ACTIONS)
                {
                    if (NextCorrection == PlanCorrectionOperation.FindingActionInPlan)
                    {
                        NextCorrection = PlanCorrectionOperation.AddingNewAction;
                    }
                }
            }

            bool TrySwitchToFindingActionInPlan(List<Action> plan)
            {
                bool result = SetNextActionInPlanIfSuitable(plan);
                if (result)
                {
                    NextCorrection = PlanCorrectionOperation.FindingActionInPlan;
                }
                return result;
            }

            protected bool SetNextActionInPlanIfSuitable(List<Action> plan)
            {
                if (parser.ALLOW_INSERTING_NEW_ACTIONS)
                {
                    nextSuitableActionInPlan = null;
                    int actionIndex = NextIndexInPlanToTry(parser);
                    return actionIndex < plan.Count && TryGetApplicableAction(new HashSet<Action> { plan[actionIndex] }, nextActionToProcess,
                        out nextSuitableActionInPlan);
                }
                else
                {
                    nextSuitableActionInPlan = null;
                    int actionIndex = NextIndexInPlanToTry(parser);

                    while (actionIndex < plan.Count)
                    {
                        nextSuitableActionInPlan = null;
                        if (TryGetApplicableAction(new HashSet<Action> { plan[actionIndex] }, nextActionToProcess,
                            out nextSuitableActionInPlan))
                        {
                            return true;
                        }

                        SkipAction();
                        actionIndex = NextIndexInPlanToTry(parser);
                    }

                    return false;
                }
            }

            protected POQueueItem IdentifyActionWithNextPlanAction(PriorityQueueWatchingFlaws queue, List<HashSet<POQueueItem>> completedStatesByFirstAction,
                List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Action> plan)
            {
                return FinishScanningWithAction(nextSuitableActionInPlan, NumberOfFlawsBeforeScanning + SkippedActions,
                    LastActionCoveredBeforeScanning + SkippedActions + 1, queue, completedStatesByFirstAction, partiallyProcessedStatesByLastAction,
                    plan, true);
            }

            protected virtual POQueueItem FinishScanningWithAction(Action action, int newNumberOfFlaws, int lastCoveredAction, PriorityQueueWatchingFlaws queue,
                List<HashSet<POQueueItem>> completedStatesByFirstAction, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Action> plan,
                bool coversActionInPrefix = false)
            {
                CFGRule cFGRule = CloneAndFillVarsBySubtaskInstantiation(CFGRule, action.ActionInstance,
                    CFGRule.CurrentSubtaskIndex, parser);
                cFGRule.IncrementCurrentSubtaskIndex();
                POQueueItem newQueueItem = parser.CreateQueueItemAndAddToTables(cFGRule, LastActionCoveredBeforeThisRule, lastCoveredAction, 
                    completedStatesByFirstAction, partiallyProcessedStatesByLastAction, plan, newNumberOfFlaws, coversActionInPrefix);
                newQueueItem.InitMinNumberOfFlawsBeforeThisDecomposition(MinNumberOfFlawsBeforeThisDecomposition);
                CompletedPredictionChildren.Add(newQueueItem);
                queue.Enqueue(newQueueItem);
                return newQueueItem;
            }

            protected POQueueItem InsertNewAction(PriorityQueueWatchingFlaws queue, HashSet<Action> allEmptyActions,
                List<HashSet<POQueueItem>> completedStatesByFirstAction, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Action> plan)
            {
                TryGetApplicableAction(allEmptyActions, nextActionToProcess, out Action action);

                if (parser.HasFixedPlanPrefix())
                {
                    if (action != null)
                    {
                        return FinishScanningWithAction(action, NumberOfFlawsBeforeScanning + SkippedActions + 1, LastActionCoveredBeforeScanning + SkippedActions,
                        queue, completedStatesByFirstAction, partiallyProcessedStatesByLastAction, plan);

                    }
                }
                else
                {
                    if (action == null)
                    {
                        throw new InvalidOperationException();
                    }

                    return FinishScanningWithAction(action, NumberOfFlawsBeforeScanning + SkippedActions + 1, LastActionCoveredBeforeScanning + SkippedActions,
                        queue, completedStatesByFirstAction, partiallyProcessedStatesByLastAction, plan);
                }

                return null;
            }

            internal enum PlanCorrectionOperation { FindingActionInPlan, AddingNewAction,
            Finished }
        }

        protected virtual bool FixedPrefix(int nextIndexInPlanToTry)
        {
            return false;
        }

        public PartialObservabilityEarleyParser(List<TaskType> allTaskTypes, List<ActionType> allActionTypes, List<Constant> allConstants,
            List<ConstantType> allConstantTypes, List<Term> initialState, bool actionInsertionAllowed = true, bool actionDeletionAllowed = true,
            bool anytimeGoals = true, bool returnFirstResult = false) : base(allTaskTypes, allActionTypes, allConstants, allConstantTypes,
                initialState)
        {
            MaxGroundingDepth = 20;
            ALLOW_INSERTING_NEW_ACTIONS = actionInsertionAllowed;
            ALLOW_DELETING_ACTIONS = actionDeletionAllowed;
            ANYTIME_GOALS = anytimeGoals;
            RETURN_FIRST_SOLUTION = returnFirstResult;
            if (RETURN_FIRST_SOLUTION)
            {
                ANYTIME_GOALS = true;
            }
        }

        protected virtual bool HasFixedPlanPrefix()
        {
            return false;
        }

        protected bool Init(List<Term> plan, List<TaskType> allTaskTypes, 
            List<ActionType> allActionTypes, List<Constant> allConstants)
        {
            InputPlanPart = new List<Subplan>();
            int i = 0;
            int size = plan.Count;
            foreach (Term a in plan)
            {
                Subplan t = CreateTaskFromAction(a, allTaskTypes, allActionTypes, i, size, allConstants);
                if (t == null)
                {
                    return false;
                }
                i++;
                InputPlanPart.Add(t);
                t.Iteration = -1; // unused
                // Initial conditions not added, an other action can be insterted before first action, must be checked later.
            }

            // Prefix timeline cannot be constructed -> dummy timeline with zero length.
            PrefixTimeline = new();
            return true;
        }

        // Only creates subplan for the task, does not check any conditions and does not propagate anything.
        static Subplan CreateTaskFromAction(Term a, List<TaskType> allTaskTypes, List<ActionType> allActions, int i, int planSize, 
            List<Constant> allConstants)
        {
            TaskType t = FindTaskType(a, allTaskTypes);
            Subplan sub = new(a, i, i, t);
            sub.usedActions = new bool[planSize];
            sub.usedActions[i] = true;
            Slot s = CreateConditionsSlot(sub, allActions, allConstants);
            SelfPropagate(s); 
            sub.SetSlot(0, s);
            t.SetMinTaskLengthIfSmaller(1);
            return sub;
        }

        internal bool RunPOPlanRecognition(List<Term> plan, List<Action> planPrefix, 
            List<Term> initialState, List<Rule> rules,
            out Rule finalRule, out Subplan finalSubplan, out List<int> addedActionsByIteration, 
            CancellationToken cancellationToken,
            IHeuristic heuristic, out List<ActionSubplan> foundPlan, Stopwatch watch, 
            out string foundGoalsWithTime)
        {
            HashSet<Action> allEmptyActions = GetEmptyActions(AllActionTypes);
            List<Rule> rulesExpandedByAllPossibleSubtaskOrderings = ExpandExplicitSubtaskOrdering(rules);
            CreateConstantTypeInstances(AllConstants, AllConstantTypes);

            if (!Init(plan, AllTaskTypes, AllActionTypes, AllConstants))
            {
                finalRule = null;
                finalSubplan = null;
                addedActionsByIteration = null;
                foundPlan = null;
                foundGoalsWithTime = String.Empty;
                return false;
            }

            Subplan subplan = RunEarleyParsing(planPrefix, AllActionTypes, rulesExpandedByAllPossibleSubtaskOrderings, AllTaskTypes,
                heuristic, cancellationToken, allEmptyActions, out foundPlan, watch, out foundGoalsWithTime);

            if (subplan == null || subplan.LastRuleInstance == null)
            {
                addedActionsByIteration = null;
                finalSubplan = null;
                finalRule = null;
                return false;
            }

            addedActionsByIteration = new List<int>(); // irrelevant here
            finalSubplan = subplan;
            finalRule = subplan.LastRuleInstance.Rule;

            return true;
        }

        protected Subplan RunEarleyParsing(List<Action> planPrefix, List<ActionType> allActionTypes, List<Rule> allRules,
            List<TaskType> allTaskTypes, IHeuristic heuristic, CancellationToken cancellationToken, HashSet<Action> allEmptyActions, out List<ActionSubplan> foundPlan, Stopwatch watch,
            out string foundGoalsWithTime)
        {
            List<HashSet<POQueueItem>> completedStatesByFirstAction = new();
            List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction = new();
            List<HashSet<POQueueItem>> cFGRulesGeneratedByPredictorByStartingIndex = new();
            TaskType dummyStartingTaskType = new("start_dummy", 0);
            AllDummyTaskTypes = new()
            {
                dummyStartingTaskType
            };
            PriorityQueueWatchingFlaws queue = InitQueue(allTaskTypes, out var dummyStartingTask, allRules, completedStatesByFirstAction,
                partiallyProcessedStatesByLastAction, planPrefix);
            AllDummyTaskTypes = new List<TaskType> { dummyStartingTask.Task.TaskType };
            PriorityQueue<QueueItemGroundingEnumerator, int> candidateGoalsByNumberOfFlaws = new();
            PriorityQueue<Tuple<QueueItemGroundingEnumerator, int, int>, int> foundCandidateGoalsWithLastVersion = new();
            Dictionary<int, QueueItemGroundingEnumerator> pendingEnumerators = new();
            Dictionary<POQueueItem, int> foundCandidateGoalItemsWithLastVersion = [];
            int counterFromLastGoalExtraction = 100;

            Subplan goal = null;
            Tuple<Subplan, int> bestGoalSoFar = null;
            List<ActionSubplan> bestPlanSoFar = null;

            StringBuilder foundGoalsSB = new();


            while (queue.Count > 0) 
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    foundPlan = bestPlanSoFar;
                    foundGoalsWithTime = foundGoalsSB.ToString();
                    return bestGoalSoFar?.Item1;
                }
#if DEBUG
                
#endif
              
                var item = queue.Dequeue();
                Debug.Assert(this is not NewPlanCorrector || item is NewPlanCorrector.ICorrectionQueueItem);

#if DEBUG
                
#endif

                if (item != null)
                {
                    if (item.IsPossibleGoal(InputPlanPart.Count, out int minNumberOfFlaws, dummyStartingTask, this)
                        )
                    {
                        foundCandidateGoalItemsWithLastVersion[item] = item.Version;
                        QueueItemGroundingEnumerator groundingEnumerator = new(new(item), this,
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

#if DEBUG
                    Console.WriteLine($"Processing item {item}");
#endif

                    item.Process(queue, planPrefix, allEmptyActions, completedStatesByFirstAction, partiallyProcessedStatesByLastAction, allRules, cFGRulesGeneratedByPredictorByStartingIndex, this);
                    counterFromLastGoalExtraction++;

                    if (counterFromLastGoalExtraction > 100)
                    {
                        CheckGoalChanges(candidateGoalsByNumberOfFlaws, ref foundCandidateGoalItemsWithLastVersion, dummyStartingTask,
                            cancellationToken, pendingEnumerators);
                        
                        if (candidateGoalsByNumberOfFlaws.Count > 0)
                        {
                            counterFromLastGoalExtraction = 0;
                        }
                    }
                    

                    int minFlawsInQueue = queue.MinNumberOfFlawsInQueue();

                    if (ANYTIME_GOALS)
                    {
                        if (bestGoalSoFar == null)
                        {
                            RecomputeCandidateGoals(candidateGoalsByNumberOfFlaws, ref foundCandidateGoalsWithLastVersion);
                           
                            TryExtractGoalWithMinNumberOfFlaws(candidateGoalsByNumberOfFlaws, ref foundCandidateGoalsWithLastVersion, pendingEnumerators,
                                int.MaxValue, ref bestGoalSoFar, cancellationToken, ref bestPlanSoFar, watch, out string foundGoals);
                            foundGoalsSB.Append(foundGoals);
                        }
                        if (bestGoalSoFar != null)
                        {
                            if (RETURN_FIRST_SOLUTION)
                            {
                                foundPlan = bestPlanSoFar;
                                foundGoalsWithTime = foundGoalsSB.ToString();
                                return bestGoalSoFar.Item1;
                            }

                            int currentCost = bestGoalSoFar.Item2;
                            for (int maxAllowedFlaws = 0; maxAllowedFlaws < currentCost; maxAllowedFlaws++)
                            {
                                RecomputeCandidateGoals(candidateGoalsByNumberOfFlaws, ref foundCandidateGoalsWithLastVersion, maxAllowedFlaws);
                         
                                TryExtractGoalWithMinNumberOfFlaws(candidateGoalsByNumberOfFlaws, ref foundCandidateGoalsWithLastVersion, pendingEnumerators,
                                    maxAllowedFlaws, ref bestGoalSoFar, cancellationToken, ref bestPlanSoFar, watch, out string foundGoals);
                                foundGoalsSB.Append(foundGoals);
                                if (currentCost > bestGoalSoFar.Item2)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    
                    if (candidateGoalsByNumberOfFlaws.TryPeek(out _, out int goalMinFlaws) && goalMinFlaws <= minFlawsInQueue
                        || bestGoalSoFar != null && bestGoalSoFar.Item2 <= minFlawsInQueue)
                    {
                        if (bestGoalSoFar != null && (candidateGoalsByNumberOfFlaws.Count == 0 || bestGoalSoFar.Item2 <= goalMinFlaws))
                        {
                            foundPlan = bestPlanSoFar;
                            foundGoalsWithTime = foundGoalsSB.ToString();

                            return bestGoalSoFar.Item1;
                        }
                        else
                        {
                            RecomputeCandidateGoals(candidateGoalsByNumberOfFlaws, ref foundCandidateGoalsWithLastVersion, queue.MinNumberOfFlawsInQueue());
                          
                            goal = TryExtractGoalWithMinNumberOfFlaws(candidateGoalsByNumberOfFlaws, ref foundCandidateGoalsWithLastVersion, pendingEnumerators,
                                queue.MinNumberOfFlawsInQueue(), ref bestGoalSoFar,  cancellationToken, ref bestPlanSoFar, watch, out string foundGoals);

                            MaxGroundingDepth++;
                            foundGoalsSB.Append(foundGoals);
                            if (goal != null)
                            {
                                foundPlan = bestPlanSoFar;
                                foundGoalsWithTime = foundGoalsSB.ToString();
                                return goal;
                            }
                        }
                    }
                }
            }

            for (int maxAllowedFlaws = 0; ; maxAllowedFlaws++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    foundPlan = bestPlanSoFar;
                    foundGoalsWithTime = foundGoalsSB.ToString();
                    return bestGoalSoFar?.Item1;
                }

                RecomputeCandidateGoals(candidateGoalsByNumberOfFlaws, ref foundCandidateGoalsWithLastVersion, maxAllowedFlaws);
                
                goal = TryExtractGoalWithMinNumberOfFlaws(candidateGoalsByNumberOfFlaws, ref foundCandidateGoalsWithLastVersion, pendingEnumerators, maxAllowedFlaws, 
                    ref bestGoalSoFar, cancellationToken, ref bestPlanSoFar, watch, out string foundGoals);

                MaxGroundingDepth += 2;
                foundGoalsSB.Append(foundGoals);

                if (goal != null)
                {
                    foundPlan = bestPlanSoFar;
                    foundGoalsWithTime = foundGoalsSB.ToString();
                    return goal;
                }
            }
        }

        protected virtual void CheckGoalChanges(PriorityQueue<QueueItemGroundingEnumerator, int> candidateGoalsByNumberOfFlaws,
            ref Dictionary<POQueueItem, int> foundCandidateGoalItemsWithLastVersion, AbstractTask dummyStartingTask,
            CancellationToken cancellationToken, Dictionary<int, QueueItemGroundingEnumerator> pendingEnumerators)
        {
            foreach (var item in foundCandidateGoalItemsReachingMaxDepthDuringLastGrounding)
            {
                item.IsPossibleGoal(InputPlanPart.Count, out int minNumberOfFlaws, dummyStartingTask, this);

                QueueItemGroundingEnumerator groundingEnumerator = new(new(item), this,
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
                foundCandidateGoalItemsReachingMaxDepthDuringLastGrounding.Remove(item);
            }
        }


        void RecomputeCandidateGoals(PriorityQueue<QueueItemGroundingEnumerator, int> candidateGoals,
            ref PriorityQueue<Tuple<QueueItemGroundingEnumerator, int, int>, int> failedCandidateGoalsWithLastVersionAndLastAllowedFlawsByNumberOfFlaws, int maxFlawsAllowed = 0)
        {
            PriorityQueue<Tuple<QueueItemGroundingEnumerator, int, int>, int> newFailedGoals = new();
            foreach (var (Element, Priority) in failedCandidateGoalsWithLastVersionAndLastAllowedFlawsByNumberOfFlaws.UnorderedItems)
            {
                if (!candidateGoals.UnorderedItems.Select(x => x.Element.Root.ID).Contains(Element.Item1.Root.ID) &&
                    (Element.Item1.Root.Version != Element.Item2 || Element.Item3 < maxFlawsAllowed))
                {
                    candidateGoals.Enqueue(Element.Item1, Priority);
                }
                else
                {
                    newFailedGoals.Enqueue(Element, Priority);
#if DEBUG
                    Console.WriteLine("\tNot added to goals: " + Element);
#endif
                }
            }
            failedCandidateGoalsWithLastVersionAndLastAllowedFlawsByNumberOfFlaws = newFailedGoals;
        }

        Subplan TryExtractGoalWithMinNumberOfFlaws(PriorityQueue<QueueItemGroundingEnumerator, int> candidateGoals,
            ref PriorityQueue<Tuple<QueueItemGroundingEnumerator, int, int>, int> failedCandidateGoalsWithLastVersionAndLastAllowedFlawsByNumberOfFlaws, 
            Dictionary<int, QueueItemGroundingEnumerator> pendingEnumerators, int maxFlawsAllowed, ref Tuple<Subplan, int> bestGoalSoFar, 
            CancellationToken cancellationToken, ref List<ActionSubplan> bestPlanSoFar, Stopwatch watch, out string foundGoalsByTime)
        {
#if DEBUG
            List<int> candidateGoalsInThisIteration = new();
            foreach (var g in candidateGoals.UnorderedItems)
            {
                candidateGoalsInThisIteration.Add(g.Element.Root.ID);
            }

            if (candidateGoals.Count > 0)
            {
                Console.Write("\tTry to extract goal from: ");
                foreach (var g in candidateGoalsInThisIteration)
                {
                    Console.Write(g + ", ");

                }
                Console.WriteLine($" (maximum grounding depth: {MaxGroundingDepth})");
            }
#endif

            CurrentMaxAllowedCost = maxFlawsAllowed;
            List<QueueItemGroundingEnumerator> usedEnumerators = new();
            Dictionary<int, Tuple<List<ActionSubplan>, List<Slot>>> currentPlans = new();
            List<QueueItemGroundingEnumerator> unusedEnumerators = new();
            StringBuilder foundGoalsSB = new StringBuilder();
            while (candidateGoals.Count > 0 && maxFlawsAllowed >= candidateGoals.Peek().NextMinNumberOfFlaws())
            {
                if (bestGoalSoFar != null)
                {
                    CurrentMaxAllowedCost = Math.Min(bestGoalSoFar.Item2, maxFlawsAllowed);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    foundGoalsByTime = foundGoalsSB.ToString();
                    return bestGoalSoFar?.Item1;
                }

                var candidateQueueItem = candidateGoals.Dequeue();
                if (pendingEnumerators.TryGetValue(candidateQueueItem.Root.ID, out var enumerator) && enumerator.ID ==
                    candidateQueueItem.ID)
                {
                    if (bestGoalSoFar == null || candidateQueueItem.Root.TotalMinNumberOfFlaws < bestGoalSoFar.Item2)
                    {

                        usedEnumerators.Add(candidateQueueItem);
                        
                        if (currentPlans.TryGetValue(candidateQueueItem.ID, out var state))
                        {
                            CurrentPlan = state.Item1;
                            CurrentTimeline = state.Item2;
                        }

                        if (candidateQueueItem.MoveNext())
                        {
                            Subplan subplan = candidateQueueItem.Current; 
                            candidateGoals.Enqueue(candidateQueueItem,
                                candidateQueueItem.NextMinNumberOfFlaws() +
                                candidateQueueItem.Root.GoalMinNumberOflaws(InputPlanPart.Count)); 
                            if (subplan != null)
                            {
                                int subplanFlaws = ComputeNumberOfFlaws(CurrentPlan);

                                if (bestGoalSoFar == null || bestGoalSoFar.Item2 > subplanFlaws)
                                {
                                    bestGoalSoFar = new Tuple<Subplan, int>(subplan, subplanFlaws);
                                    bestPlanSoFar = new List<ActionSubplan>(CurrentPlan);
                                    long t = watch.ElapsedMilliseconds;
                                    if (bestGoalSoFar.Item1.LastRuleInstance != null)
                                    {
                                        Console.WriteLine($"\t\tFOUND GOAL: {bestGoalSoFar.Item1} FLAWS: {bestGoalSoFar.Item2} TIME: {t} ms");
                                        WriteSolution(bestGoalSoFar.Item1, bestPlanSoFar);
                                        if (RETURN_FIRST_SOLUTION)
                                        {
                                            foundGoalsByTime = foundGoalsSB.ToString();
                                            return bestGoalSoFar.Item1;
                                        }
#if DEBUG
                                        Console.WriteLine($"\t\t\t{candidateQueueItem}");
#endif
                                        foundGoalsSB.Append($"({bestGoalSoFar.Item1}, {bestGoalSoFar.Item2}, {t}), ");
                                    }
                                }

                                if (maxFlawsAllowed >= bestGoalSoFar.Item2)
                                {
                                    if (!candidateGoals.TryPeek(out _, out int bestCandidateFlaws) || bestGoalSoFar.Item2 <= bestCandidateFlaws)
                                    {
                                        foundGoalsByTime = foundGoalsSB.ToString();
                                        return bestGoalSoFar.Item1;
                                    }
                                }
                            }
                        }
                        else if (lastGroundingReachedMaxDepth)
                        {
                            foundCandidateGoalItemsReachingMaxDepthDuringLastGrounding.Add(candidateQueueItem.Root);
                            lastGroundingReachedMaxDepth = false;
                        }


                            currentPlans[candidateQueueItem.ID] = new(CurrentPlan, CurrentTimeline);
                        ResetCurrentPlan();
                    }
                    else
                    {
                        unusedEnumerators.Add(candidateQueueItem);
                    }

                }
            }

            foreach (var enumerator in usedEnumerators)
            {
                enumerator.Reset();  
                failedCandidateGoalsWithLastVersionAndLastAllowedFlawsByNumberOfFlaws.Enqueue(new(enumerator, enumerator.Root.Version, maxFlawsAllowed), enumerator.Root.GoalMinNumberOflaws(InputPlanPart.Count));               
               
                pendingEnumerators[enumerator.Root.ID] = enumerator;
            }

            foreach (var enumerator in unusedEnumerators)
            {
                candidateGoals.Enqueue(enumerator, enumerator.Root.GoalMinNumberOflaws(InputPlanPart.Count));
            }

            if (bestGoalSoFar != null && bestGoalSoFar.Item2 <= CurrentMaxAllowedCost)
            {
                foundGoalsByTime = foundGoalsSB.ToString();
                return bestGoalSoFar.Item1;
            }
            else
            {
                foundGoalsByTime = foundGoalsSB.ToString();
                return null;
            }
        }

        protected virtual void WriteSolution(Subplan bestGoalSoFar, List<ActionSubplan> bestPlanSoFar)
        {
            EntryPoint.WritePOSolution(bestGoalSoFar, bestPlanSoFar, this, ALLOW_INSERTING_NEW_ACTIONS, ALLOW_DELETING_ACTIONS, ANYTIME_GOALS, out _, out _, out _, "\t\t\t\t", false);

        }

        internal virtual int ComputeNumberOfFlaws(List<ActionSubplan> foundPlan)
        {
           
            int usedActionsFromPlan = foundPlan.Count(x => x.IsInPlan);
            int inserted = foundPlan.Count(x => !x.IsInPlan);
            int deleted = InputPlanPart.Count - usedActionsFromPlan;
            return deleted + inserted;
        }

        protected override List<Subplan> RedistributeSubtasks(List<Subplan> groundedSubtasks) // expects continuous subplan
        {
            List<Subplan> result = new();
            foreach (var subplan in groundedSubtasks)
            {
                result.Add(subplan.Copy());
            }
            for (int i = 1; i < groundedSubtasks.Count; i++)
            {
                double length = result[i].EndIndex - result[i].StartIndex;
                result[i].StartIndex = result[i - 1].EndIndex + 1;
                result[i].EndIndex = result[i].StartIndex + length;
            }
            return result;
        }

        

        protected virtual PriorityQueueWatchingFlaws InitQueue(List<TaskType> allTaskTypes, out AbstractTask dummyStartingTask, List<Rule> allRules,
            List<HashSet<POQueueItem>> completedStatesByFirstAction, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Action> plan) 
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
                POQueueItem queueItem = CreateQueueItemAndAddToTables(dummyCFGRule, 0, 0, completedStatesByFirstAction,
                    partiallyProcessedStatesByLastAction, plan);
                queue.Enqueue(queueItem);
            }

            return queue;
        }

        protected static HashSet<Action> GetEmptyActions(List<ActionType> allActionTypes)
        {
            var allActions = new HashSet<Action>(allActionTypes.Count);
            foreach (ActionType actionType in allActionTypes)
            {
                allActions.Add(new Action(actionType));
            }
            return allActions;
        }
    }
}
