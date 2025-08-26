**Contains the implementation of hierarchical plan correction by top-down parsing inspired by the Earley parser and benchmarks from [^1].**

### Implementation
Language and framework: C# 13.0, .NET 9.0

Plan correction can be executed as follows:
`EarleyPlanCorrector.exe domain_file problem_file invalid_plan_file csv_result_file po2 timeout_in_seconds action_insertion_allowed=y/n action_deletion_allowed=y/n anytime_solutions=y/n`

Example - plan recognition with full observability and noise (only action deletion is allowed) with anytime solutions:
`EarleyPlanCorrector.exe domain_file problem_file invalid_plan_file csv_result_file po2 timeout_in_seconds n y y`

The program will write its output on the standard output and into the given csv file.

The implementation extends the implementation of the bottom-up parsing-based HTN plan recognition from [^2].

### Benchmarks
The domains are from the International planning competition (IPC) 2020[^3]. Invalid plans to be corrected were created by modifying valid plans generated in IPC 2020[^4].

Each directory corresponds to one HTN planning domain and contains domain files in the directory `domains`, problem files in the directory `problems` and invalid plans in the directory `plan_correction`, which contains four subdirectories: `original_plans` are the original valid IPC plans, `plans_with_added_actions` are plans modified by inserting extra actions (benchmarks for action deletion), `plans_with_deleted_action` (for action insertion) and `plans_with_mixed_corrections` (for plan correction by action insertion and deletion).

[^1]: Pantůčková, Kristýna; Barták, Roman. What to Do if a Plan Does Not Comply with an HTN Model? In: The European Conference on Artificial Intelligence (ECAI). 2025.
[^2]: Ondrčková, Simona. Validation and Recognition of Hierarchical Plans. Master's thesis, Faculty of Mathematics and Physics, Charles University. 2020.
[^3]: https://github.com/panda-planner-dev/ipc2020-domains
[^4]: https://github.com/panda-planner-dev/ipc-2020-plans/tree/master