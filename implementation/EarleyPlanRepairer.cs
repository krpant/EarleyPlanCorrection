using PlanRecognitionNETF;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlanRecognitionExtension
{
    internal class EarleyPlanRepairer : PartialObservabilityEarleyParser//EarleyParser
    {
        protected List<Term> allPredicates;
        protected List<Rule> allRules;
        protected List<Task> toGoals;
        protected List<Term> positiveStateChanges;
        protected List<Term> negativeStateChanges;
        protected readonly string specialEffectTermName = "PLAN_REPAIR_SPECIAL_EFFECT";
        internal const string STATE_CHANGE_ACTION_NAME = "STATE-CHANGE";
        protected readonly string specialTaskName = "PLAN_REPAIR_SPECIAL_TASK";
        protected Action specialActionForStateChange;

        public EarleyPlanRepairer(List<TaskType> allTaskTypes, List<ActionType> allActionTypes, 
            List<Constant> allConstants,
            List<ConstantType> allConstantTypes, List<Term> initialState,
            List<Task> toGoals,
            List<Term> allPredicates, List<Rule> allRules) : base(allTaskTypes, allActionTypes,
                allConstants, allConstantTypes, initialState)
        {
            this.allPredicates = allPredicates;
            this.allRules = allRules;
            this.toGoals = toGoals;
        }


        protected bool InitPlanRepair(List<Term> plan, List<Action> planPrefix,
            List<Term> positiveStateChanges, List<Term> negativeStateChanges,
            out List<Rule> rulesExpandedByAllPossibleSubtaskOrderings)
        {
            this.positiveStateChanges = positiveStateChanges;
            this.negativeStateChanges = negativeStateChanges;
            ActionType newActionType = IntroduceNewSpecialAction(planPrefix[^1].ActionType);
            specialActionForStateChange = new(newActionType);
            rulesExpandedByAllPossibleSubtaskOrderings =
                ExpandExplicitSubtaskOrdering(allRules);
            CreateConstantTypeInstances(AllConstants, AllConstantTypes);

            List<Term> modifiedPlan = new(plan) { newActionType.ActionTerm };
            List<Action> newPlanPrefix = new(planPrefix) { new(newActionType) };
            //planPrefix = newPlanPrefix;

            if (!Init(modifiedPlan, AllTaskTypes, AllActionTypes, AllConstants, InitialState))
            {
                return false;
            }

            return true;
        }

        internal bool RepairPlan(List<Term> plan, List<Action> planPrefix, 
            List<Term> positiveStateChanges, List<Term> negativeStateChanges,
            out Rule finalRule, out Subplan finalSubtask, 
            out List<int> addedActionsByIteration, 
            CancellationToken cancellationToken)
        {
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
                finalSubtask = null;
                addedActionsByIteration = null;
                return false;
            }
            //if (!InitPlanRepair(plan, planPrefix, positiveStateChanges, negativeStateChanges,
            //    out List<Rule> rulesExpandedByAllPossibleSubtaskOrderings))
            //{
            //    finalRule = null;
            //    finalSubtask = null;
            //    addedActionsByIteration = null;
            //    return false;
            //}

            Subplan subplan = RunEarleyParsingAsPlanRecognition(planPrefix, AllActionTypes, 
                rulesExpandedByAllPossibleSubtaskOrderings, AllTaskTypes,
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                finalRule = null;
                finalSubtask = null;
                addedActionsByIteration = null;
                return false;
            }

            addedActionsByIteration = new List<int>(); // irrelevant here
            finalSubtask = subplan;
            finalRule = subplan.LastRuleInstance.Rule;
            return true;
            //this.positiveStateChanges = positiveStateChanges;
            //this.negativeStateChanges = negativeStateChanges;
            //ActionType newActionType = IntroduceNewSpecialAction(planPrefix[^1].ActionType);
            //specialActionForStateChange = new(newActionType);
            //List<Rule> rulesExpandedByAllPossibleSubtaskOrderings =
            //    ExpandExplicitSubtaskOrdering(allRules);
            //CreateConstantTypeInstances(AllConstants, AllConstantTypes);

            //List<Term> modifiedPlan = new(plan) { newActionType.ActionTerm };
            //List<Action> newPlanPrefix = new(planPrefix) { new(newActionType) };
            //planPrefix = newPlanPrefix;

            //if (!Init(modifiedPlan, AllTaskTypes, AllActionTypes, AllConstants, InitialState))
            //{
            //    finalRule = null;
            //    finalSubtask = null;
            //    addedActionsByIteration = null;
            //    return false;
            //}

            ////if (!InitPlanRepair(plan, ref planPrefix, positiveStateChanges, negativeStateChanges,
            ////    out List<Rule> rulesExpandedByAllPossibleSubtaskOrderings))
            ////{
            ////    finalRule = null;
            ////    finalSubtask = null;
            ////    addedActionsByIteration = null;
            ////    return false;
            ////}

            //Subplan subplan = RunEarleyParsingAsPlanRecognition(planPrefix, AllActionTypes, rulesExpandedByAllPossibleSubtaskOrderings, AllTaskTypes,
            //        cancellationToken);

            //if (cancellationToken.IsCancellationRequested)
            //{
            //    finalRule = null;
            //    finalSubtask = null;
            //    addedActionsByIteration = null;
            //    return false;
            //}

            //addedActionsByIteration = new List<int>(); // irrelevant here
            //finalSubtask = subplan;
            //finalRule = subplan.LastRuleInstance.Rule;
            //return true;
        }

        protected override void InitializeQueue(Queue<EarleyParser.QueueItem> queue, List<TaskType> allTaskTypes, out AbstractTask dummyStartingTask, List<Rule> allRules, out List<Rule> allRulesExtended, HashSet<EarleyParser.QueueItem> queueHashSet, Task rootTask = null)
        {
            allRulesExtended = new List<Rule>(allRules);
            TaskType dummyStartingTaskType = new TaskType("start_dummy", 0);
            dummyStartingTask = new AbstractTask(new Task(dummyStartingTaskType));

            TaskType secondDummyTaskType = new TaskType("second_dummy", 0);
            AbstractTask dummySecondTask = new AbstractTask(new Task(secondDummyTaskType));

            AllDummyTaskTypes = new List<TaskType> { dummyStartingTaskType, secondDummyTaskType };

            CFGRule startingRule = new CFGRule(dummyStartingTask, 
                toGoals.Select(x => new AbstractTask(x)).ToArray(), new Rule
            {
                MainTaskType = dummyStartingTaskType,
                TaskTypeArray = toGoals.Select(x => x.TaskType).ToArray(),
                ArrayOfReferenceLists = 
                    Enumerable.Repeat(new List<int>(0), toGoals.Count).ToArray(),
                MainTaskReferences = new List<int>(0)
            }, this);
            EarleyParser.QueueItem queueItem = new EarleyParser.QueueItem(startingRule, 0);
            queue.Enqueue(queueItem);
            queueHashSet.Add(queueItem);
        }

        protected override List<HashSet<Action>> GetInitialDomains(List<Action> planPrefix, 
            int suffixLength, List<ActionType> allActionTypes, out HashSet<Action> allActions)
        {
            List<HashSet<Action>> result = new List<HashSet<Action>>(planPrefix.Count +
                suffixLength);

            for (int i = 0; i < planPrefix.Count; i++)
            {
                result.Add(new HashSet<Action>() { planPrefix[i] });
            }
            result.Add(new() { specialActionForStateChange });

            allActions = new HashSet<Action>(allActionTypes.Count);
            foreach (ActionType actionType in allActionTypes)
            {
                if (actionType != specialActionForStateChange.ActionType)
                {
                    allActions.Add(new Action(actionType));
                }
            }

            for (int i = planPrefix.Count; i < planPrefix.Count + suffixLength; i++)
            {
                result.Add(allActions);
            }

            return result;
        }

            protected ActionType IntroduceNewSpecialAction(ActionType lastPrefixActionType)
        {
            ActionType newActionType = CreateNewSpecialAction();
            AllActionTypes = new(AllActionTypes)
            {
                newActionType
            };
            ActionTaskType newActionTaskType = new(STATE_CHANGE_ACTION_NAME,
                newActionType.ActionTerm.Variables.Length, newActionType);
            //{
            //    TaskTerm = new(STATE_CHANGE_ACTION_NAME, newActionType.ActionTerm.Variables)
            //};

            TaskType newTaskType = new(specialTaskName,
                lastPrefixActionType.ActionTerm.Variables.Length)
            {
                TaskTerm = new Term(specialTaskName, lastPrefixActionType.ActionTerm.Variables)
            };
            AllTaskTypes = new(AllTaskTypes)
            {
                newTaskType,
                newActionTaskType
            };

            ModifyTaskHierarchy(newTaskType, lastPrefixActionType, newActionTaskType);
            return newActionType;
        }

        private void AddNewRules(TaskType originalLastActionTaskType, 
            ActionTaskType newActionTaskType, TaskType newTaskType)
        {
            Rule rule1 = new()
            {
                MainTaskType = newTaskType,
                TaskTypeArray = new TaskType[1] { originalLastActionTaskType },
                AllVarsTypes = 
                originalLastActionTaskType.TaskTerm.Variables.Select(x => x.Type).ToList(),
                AllVars =
                originalLastActionTaskType.TaskTerm.Variables.Select(x => x.Name).ToList(),
                MainTaskReferences = 
                Enumerable.Range(0, originalLastActionTaskType.NumOfVariables).ToList(),
                ArrayOfReferenceLists = new List<int>[1] {
                Enumerable.Range(0, originalLastActionTaskType.NumOfVariables).ToList()}
            };

            TaskType abstractNewActionTaskType = new(newActionTaskType.Name,
                newActionTaskType.NumOfVariables);
            AllTaskTypes.Add(abstractNewActionTaskType);

            Rule rule2 = new()
            {
                MainTaskType = newTaskType,
                TaskTypeArray = new TaskType[2] { originalLastActionTaskType,
                abstractNewActionTaskType },
                AllVarsTypes =
                originalLastActionTaskType.TaskTerm.Variables.Select(x => x.Type).ToList(),
                AllVars =
                originalLastActionTaskType.TaskTerm.Variables.Select(x => x.Name).ToList(),
                MainTaskReferences =
                Enumerable.Range(0, originalLastActionTaskType.NumOfVariables).ToList(),
                ArrayOfReferenceLists = new List<int>[2] {
                Enumerable.Range(0, originalLastActionTaskType.NumOfVariables).ToList(),
                new()},
                orderConditions = new() { new(0, 1)}
            };

            Rule rule3 = new()
            {
                MainTaskType = abstractNewActionTaskType,
                TaskTypeArray = new TaskType[1] { newActionTaskType },
                AllVarsTypes = new(),
                AllVars = new(),
                MainTaskReferences = new(),
                ArrayOfReferenceLists = new List<int>[1] { new() }
            };

            allRules.Add(rule1);
            allRules.Add(rule2);
            allRules.Add(rule3);
        }

        protected override Subplan GoalTask(List<Subplan> groundedSubtasks)
        {
            return EarleyPlanner.GetSingleGoalTaskFromSequence(groundedSubtasks);
        }

        private void ModifyTaskHierarchy(TaskType newTaskType, 
            ActionType originalLastActionType, 
            ActionTaskType newActionTaskType)
        {
            allRules = new(allRules);
            TaskType originalLastActionTaskType =
                FindTaskType(originalLastActionType.ActionTerm, AllTaskTypes);
            ModifyExistingRules(originalLastActionTaskType, newTaskType);
            AddNewRules(originalLastActionTaskType, newActionTaskType, newTaskType);
        }

        private void ModifyExistingRules(TaskType originalLastActionTaskType, 
            TaskType newTaskType)
        {
            foreach (var rule in allRules)
            {
                for (int i = 0; i < rule.TaskTypeArray.Length; i++)
                {
                    TaskType subtask = rule.TaskTypeArray[i];
                    if (subtask == originalLastActionTaskType)
                    {
                        rule.TaskTypeArray[i] = newTaskType;
                    }
                }
            }
        }

        private ActionType CreateNewSpecialAction()
        {
            ActionType newSpecialActionType = new ActionType();
            //newSpecialActionType.NegPreConditions.Add(new(specialEffectTermName, new() { -3 }));
            //newSpecialActionType.PosPostConditions.Add(new(specialEffectTermName, new() { -3 }));

            //List<Constant> parameters = new();
            //foreach (var posTerm in positiveStateChanges)
            //{
            //    List<int> constantIndexes = new();
            //    foreach (var constant in posTerm.Variables)
            //    {
            //        int paramIndex = parameters.IndexOf(constant);
            //        if (paramIndex == -1)
            //        {
            //            parameters.Add(constant);
            //            paramIndex = parameters.Count - 1;
            //        }
            //        constantIndexes.Add(paramIndex);
            //    }
            //    newSpecialActionType.PosPostConditions.Add(new(posTerm.Name, constantIndexes));
            //}

            //foreach (var negTerm in negativeStateChanges)
            //{
            //    List<int> constantIndexes = new();
            //    foreach (var constant in negTerm.Variables)
            //    {
            //        int paramIndex = parameters.IndexOf(constant);
            //        if (paramIndex == -1)
            //        {
            //            parameters.Add(constant);
            //            paramIndex = parameters.Count - 1;
            //        }
            //        constantIndexes.Add(paramIndex);
            //    }
            //    newSpecialActionType.NegPostConditions.Add(new(negTerm.Name, constantIndexes));
            //}

            //newSpecialActionType.ActionTerm = new(STATE_CHANGE_ACTION_NAME,
            //    parameters.ToArray());

            foreach (var posTerm in positiveStateChanges)
            {
                List<int> constantIndexes = new();
                foreach (var constant in posTerm.Variables)
                {
                    constantIndexes.Add(-3);
                }
                string termName = string.Join('!', (new string[1] { posTerm.Name }).
                    Concat(posTerm.Variables.Select(x => x.Name)).ToArray());
                newSpecialActionType.PosPostConditions.Add(new(termName,
                    constantIndexes));
            }

            foreach (var negTerm in negativeStateChanges)
            {
                List<int> constantIndexes = new();
                foreach (var constant in negTerm.Variables)
                {
                    constantIndexes.Add(-3);
                }
                string termName = string.Join('!', (new string[1] { negTerm.Name }).
                    Concat(negTerm.Variables.Select(x => x.Name)).ToArray());
                newSpecialActionType.NegPostConditions.Add(new(termName, constantIndexes));
            }

            newSpecialActionType.ActionTerm = new(STATE_CHANGE_ACTION_NAME,
                Array.Empty<Constant>());
            return newSpecialActionType;
        }
    }
}
