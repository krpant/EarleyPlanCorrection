using PlanRecognitionNETF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static PlanRecognitionExtension.PartialObservabilityEarleyParser;
namespace PlanRecognitionExtension
{
    internal class PlanCorrectionRepairer : EarleyPlanRepairer

    {
        int prefixLength;
        public PlanCorrectionRepairer(List<TaskType> allTaskTypes, 
            List<ActionType> allActionTypes, List<Constant> allConstants, 
            List<ConstantType> allConstantTypes, List<Term> initialState, List<Task> toGoals,
            List<Term> allPredicates, List<Rule> allRules,
            bool anytimeGoals) : base(allTaskTypes, 
                allActionTypes, allConstants, allConstantTypes, initialState, toGoals, 
                allPredicates, allRules) 
        {
            ANYTIME_GOALS = anytimeGoals;
            RETURN_FIRST_SOLUTION = false;
        }

        protected override bool FixedPrefix(int nextIndexInPlanToTry)
        {
            return nextIndexInPlanToTry < prefixLength; 
        }

        protected override bool HasFixedPlanPrefix()
        {
            return true;
        }

        protected override PriorityQueueWatchingFlaws InitQueue(List<TaskType> allTaskTypes, out AbstractTask dummyStartingTask, List<Rule> allRules, List<HashSet<POQueueItem>> completedStatesByFirstAction, List<HashSet<POQueueItem>> partiallyProcessedStatesByLastAction, List<Action> plan)
        {
            PriorityQueueWatchingFlaws queue = new();
            TaskType dummyStartingTaskType = new("start_dummy", 0);
            AllDummyTaskTypes = new()
            {
                dummyStartingTaskType
            };
            dummyStartingTask = new AbstractTask(new Task(dummyStartingTaskType));

            List<int>[] arrayOfReferenceLists = new List<int>[toGoals.Count];

            int varIndex = 0;
            for (int i = 0; i < toGoals.Count; i++)
            {
                Task g = toGoals[i];
                List<int> referenceList = Enumerable.Range(varIndex, g.TaskType.NumOfVariables).ToList();
                varIndex += referenceList.Count;
                arrayOfReferenceLists[i] = referenceList;
            }

            Rule dummyRule = new()
            {
                MainTaskType = dummyStartingTaskType,
                TaskTypeArray = toGoals.Select(x => x.TaskType).ToArray(),
                ArrayOfReferenceLists = arrayOfReferenceLists,
                MainTaskReferences = new List<int>(0)
            };

            CFGTask[] cFGSubtasks = toGoals.Select(x => new AbstractTask(x) as CFGTask).ToArray();
            CFGRule dummyCFGRule = new(dummyStartingTask, cFGSubtasks, dummyRule, this);
            POQueueItem queueItem = CreateQueueItemAndAddToTables(dummyCFGRule, 0, 0, completedStatesByFirstAction,
                partiallyProcessedStatesByLastAction, plan);
            queue.Enqueue(queueItem);
            return queue;
        }

        internal bool RepairPlanWithoutSuffix(List<Term> plan, List<Action> planPrefix,
            List<Term> positiveStateChanges, List<Term> negativeStateChanges,
            out Rule finalRule, out Subplan finalSubplan, out List<int> addedActionsByIteration,
            out List<ActionSubplan> foundPlan, Stopwatch watch,
            out string foundGoalsWithTime,
            CancellationToken cancellationToken)
        {
            prefixLength = plan.Count + 1;
            ALLOW_DELETING_ACTIONS = false;
            ALLOW_INSERTING_NEW_ACTIONS = true;

            HashSet<Action> allEmptyActions = GetEmptyActions(AllActionTypes);
            this.positiveStateChanges = positiveStateChanges;
            this.negativeStateChanges = negativeStateChanges;
            ActionType newActionType = IntroduceNewSpecialAction(planPrefix[^1].ActionType);
            specialActionForStateChange = new(newActionType);
            List<Rule> rulesExpandedByAllPossibleSubtaskOrderings =
                ExpandExplicitSubtaskOrdering(allRules);
            CreateConstantTypeInstances(AllConstants, AllConstantTypes);

            List<Term> modifiedPlan = new(plan) { newActionType.ActionTerm };
            List<Action> newPlanPrefix = new(planPrefix) { new(newActionType) };

            if (!Init(modifiedPlan, AllTaskTypes, AllActionTypes, AllConstants, InitialState))
            {
                finalRule = null;
                finalSubplan = null;
                addedActionsByIteration = null;
                foundPlan = null;
                foundGoalsWithTime = null;
                return false;
            }

            //if (InitPlanRepair(plan, planPrefix, positiveStateChanges, negativeStateChanges,
            //    out rulesExpandedByAllPossibleSubtaskOrderings))
            //{
            if (cancellationToken.IsCancellationRequested)
            {
                finalRule = null;
                finalSubplan = null;
                addedActionsByIteration = null;
                foundPlan = null;
                foundGoalsWithTime = String.Empty;
                return false;
            }


            Subplan subplan = RunEarleyParsing(newPlanPrefix, AllActionTypes,
                    rulesExpandedByAllPossibleSubtaskOrderings, AllTaskTypes,
                new MinFlawsIncludingUncoveredActionsHeuristic(prefixLength),
                cancellationToken,
                allEmptyActions,
                out foundPlan, watch, out foundGoalsWithTime);


            if (subplan == null || subplan.LastRuleInstance == null)
            {
                addedActionsByIteration = null;
                finalSubplan = null;
                finalRule = null;
                return false;
            }
            else
            {
                addedActionsByIteration = new List<int>(); // irrelevant here
                finalSubplan = subplan;
                finalRule = subplan.LastRuleInstance.Rule;
            }

            return true;
            //}

            finalRule = null;
            finalSubplan = null;
            addedActionsByIteration = null;
            foundPlan = null;
            foundGoalsWithTime = String.Empty;
            return false;
        }

        internal bool RepairPlanWithSuffix(List<Term> plan, List<Action> planPrefix,
            List<Term> positiveStateChanges, List<Term> negativeStateChanges,
            out Rule finalRule, out Subplan finalSubplan, out List<int> addedActionsByIteration,
            out List<ActionSubplan> foundPlan, Stopwatch watch,
            out string foundGoalsWithTime,
            CancellationToken cancellationToken)
        {
            prefixLength = ComputePrefixLength(planPrefix);
            ALLOW_DELETING_ACTIONS = true;
            ALLOW_INSERTING_NEW_ACTIONS = true;
            HashSet<Action> allEmptyActions = GetEmptyActions(AllActionTypes);

            if (InitPlanRepairWithSuffix(plan, planPrefix, positiveStateChanges, negativeStateChanges,
                out List<Rule> rulesExpandedByAllPossibleSubtaskOrderings))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    finalRule = null;
                    finalSubplan = null;
                    addedActionsByIteration = null;
                    foundPlan = null;
                    foundGoalsWithTime = String.Empty;
                    return false;
                }

                Subplan subplan = RunEarleyParsing(planPrefix, AllActionTypes,
                    rulesExpandedByAllPossibleSubtaskOrderings, AllTaskTypes,
                new MinFlawsIncludingUncoveredActionsHeuristic(prefixLength),
                cancellationToken,
                allEmptyActions,
                out foundPlan, watch, out foundGoalsWithTime);

                addedActionsByIteration = new List<int>(); // irrelevant here
                finalSubplan = subplan;

                if (subplan == null || subplan.LastRuleInstance == null)
                {
                    addedActionsByIteration = null;
                    finalSubplan = null;
                    finalRule = null;
                    return false;
                }
                else
                {
                    finalRule = subplan.LastRuleInstance.Rule;
                }

                return true;
            }

            finalRule = null;
            finalSubplan = null;
            addedActionsByIteration = null;
            foundPlan = null;
            foundGoalsWithTime = String.Empty;
            return false;
        }

        private bool InitPlanRepairWithSuffix(List<Term> plan, List<Action> planPrefix, List<Term> positiveStateChanges, List<Term> negativeStateChanges, out List<Rule> rulesExpandedByAllPossibleSubtaskOrderings)
        {
            this.positiveStateChanges = positiveStateChanges;
            this.negativeStateChanges = negativeStateChanges;
            ActionType newActionType = 
                IntroduceNewSpecialAction(planPrefix[prefixLength - 2].ActionType);
            specialActionForStateChange = new(newActionType);
            rulesExpandedByAllPossibleSubtaskOrderings =
                ExpandExplicitSubtaskOrdering(allRules);
            CreateConstantTypeInstances(AllConstants, AllConstantTypes);

            List<Term> modifiedPlan = new();
            List<Action> newPlanPrefix = new();

            for (int i = 0; i < plan.Count; i++)
            {
                if (plan[i].Name.ToLower() == EarleyPlanRepairer.STATE_CHANGE_ACTION_NAME.ToLower())
                {
                    modifiedPlan.Add(newActionType.ActionTerm);
                    newPlanPrefix.Add(new(newActionType));
                }
                else
                {
                    modifiedPlan.Add(plan[i]);
                    newPlanPrefix.Add(planPrefix[i]);
                }
            }

            if (!Init(modifiedPlan.GetRange(0, prefixLength), 
                AllTaskTypes, AllActionTypes, AllConstants, InitialState)
                || !Init(modifiedPlan, AllTaskTypes, AllActionTypes, AllConstants))
            {
                return false;
            }



            return true;
        }

        private int ComputePrefixLength(List<Action> planPrefix)
        {
            int length = 0;

            for (int i = 0; i < planPrefix.Count; i++)
            {
                length++;
                if (planPrefix[i].ActionType.ActionTerm.Name == STATE_CHANGE_ACTION_NAME)
                {
                    break;
                }
            }

            return length;
        }
    }
}
