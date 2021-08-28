using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sharpen;

namespace Sharplog.Engine
{
    public class Engine
    {
        public static IList<Expr> ReorderQuery(IList<Expr> query)
        {
            IList<Expr> ordered = new List<Expr>(query.Count);
            foreach (Expr e in query)
            {
                if (!e.IsNegated() && !(e.IsBuiltIn() && !e.GetPredicate().Equals("=")))
                {
                    ordered.Add(e);
                }
            }
            // Note that a rule like s(A, B) :- r(A, B), X = Y, q(Y), A > X. will cause an error relating to both sides
            // of the '=' being unbound, and it can be fixed by moving the '=' operators to here, but I've decided against
            // it, because the '=' should be evaluated ASAP, and it is difficult to determine programatically when that is.
            // The onus is thus on the user to structure '=' operators properly.
            foreach (Expr e in query)
            {
                if (e.IsNegated() || (e.IsBuiltIn() && !e.GetPredicate().Equals("=")))
                {
                    ordered.Add(e);
                }
            }
            return ordered;
        }

        /// <exception cref="Sharplog.DatalogException"/>
        public static IList<IList<Rule>> ComputeStratification(IList<Rule> allRules)
        {
            List<IList<Rule>> strata = new List<IList<Rule>>(10);
            IDictionary<string, int?> strats = new Dictionary<string, int?>();
            foreach (Rule rule in allRules)
            {
                string pred = rule.GetHead().GetPredicate();
                int? stratum = strats.GetOrNullable(pred);
                if (stratum == null)
                {
                    stratum = DepthFirstSearch(rule.GetHead(), allRules, new List<Expr>(), 0);
                    strats[pred] = stratum;
                }
                while (stratum.Value >= strata.Count)
                {
                    strata.Add(new List<Rule>());
                }
                strata[stratum.Value].Add(rule);
            }
            strata.Add(allRules);
            return strata;
        }

        /* Reorganize the goals in a query so that negated literals are at the end.
        A rule such as `a(X) :- not b(X), c(X)` won't work if the `not b(X)` is evaluated first, since X will not
        be bound to anything yet, meaning there are an infinite number of values for X that satisfy `not b(X)`.
        Reorganising the goals will solve the problem: every variable in the negative literals will have a binding
        by the time they are evaluated if the rule is /safe/, which we assume they are - see Rule#validate()
        Also, the built-in predicates (except `=`) should only be evaluated after their variables have been bound
        for the same reason; see [ceri] for more information. */
        /* Computes the stratification of the rules in the IDB by doing a depth-first search.
        * It throws a DatalogException if there are negative loops in the rules, in which case the
        * rules aren't stratified and cannot be computed. */
        /* The recursive depth-first method that computes the stratification of a set of rules */

        protected internal static IEnumerable<string> GetRelevantPredicates(Sharplog.Jatalog jatalog, IList<Expr> originalGoals)
        {
            HashSet<string> relevant = new HashSet<string>();
            List<Expr> goals = new List<Expr>(originalGoals);
            while (!(goals.Count == 0))
            {
                Expr expr = goals[0];
                goals.RemoveAt(0);
                if (!relevant.Contains(expr.GetPredicate()))
                {
                    relevant.Add(expr.GetPredicate());
                    foreach (Rule rule in jatalog.GetIdb())
                    {
                        if (rule.GetHead().GetPredicate().Equals(expr.GetPredicate()))
                        {
                            goals.AddRange(rule.GetBody());
                        }
                    }
                }
            }
            return relevant;
        }

        protected internal static IDictionary<int, List<Rule>> BuildDependentRules(IEnumerable<Rule> rules)
        {
            IDictionary<int, List<Rule>> map = new Dictionary<int, List<Rule>>();
            foreach (Rule rule in rules)
            {
                foreach (Expr goal in rule.GetBody())
                {
                    List<Rule> dependants = map.GetOrNull(goal.GetPredicate().GetHashCode());
                    if (dependants == null)
                    {
                        dependants = new List<Rule>();
                        map[goal.GetPredicate().GetHashCode()] = dependants;
                    }
                    if (!dependants.Contains(rule))
                    {
                        dependants.Add(rule);
                    }
                }
            }
            return map;
        }

        protected internal static IEnumerable<Rule> GetDependentRules(IndexedSet<Expr> facts, IDictionary<int, List<Rule>> dependents)
        {
            HashSet<Rule> dependantRules = new HashSet<Rule>();
            foreach (int predicate in facts.GetIndexes())
            {
                IEnumerable<Rule> rules = dependents.GetOrNull(predicate);
                if (rules != null)
                {
                    dependantRules.UnionWith(rules);
                }
            }
            return dependantRules;
        }

        protected internal static IEnumerable<IDictionary<string, string>> MatchGoals(IList<Expr> goals, IndexedSet<Expr> facts, StackMap<string, string> bindings)
        {
            Expr goal = goals[0];
            // First goal; Assumes goals won't be empty
            bool lastGoal = (goals.Count == 1);
            if (goal.IsBuiltIn())
            {
                StackMap<string, string> newBindings = new StackMap<string, string>(bindings);
                bool eval = goal.EvalBuiltIn(newBindings);
                if (eval && !goal.IsNegated() || !eval && goal.IsNegated())
                {
                    if (lastGoal)
                    {
                        return System.Linq.Enumerable.ToList(new[] { newBindings.Map });
                    }
                    else
                    {
                        return MatchGoals(goals.Skip(1).ToList(), facts, newBindings);
                    }
                }
                return new System.Collections.Generic.List<IDictionary<string, string>>();
            }
            List<IDictionary<string, string>> answers = new List<IDictionary<string, string>>();
            if (!goal.IsNegated())
            {
                // Positive rule: Match each fact to the first goal.
                // If the fact matches: If it is the last/only goal then we can return the bindings
                // as an answer, otherwise we recursively check the remaining goals.
                foreach (Expr fact in facts.GetIndexed(goal.GetPredicate().GetHashCode()))
                {
                    StackMap<string, string> newBindings = new StackMap<string, string>(bindings);
                    if (fact.Unify(goal, newBindings))
                    {
                        if (lastGoal)
                        {
                            answers.Add(newBindings.Map);
                        }
                        else
                        {
                            // More goals to match. Recurse with the remaining goals.
                            answers.AddRange(MatchGoals(goals.Skip(1).ToList(), facts, newBindings));
                        }
                    }
                }
            }
            else
            {
                // Negated rule: If you find any fact that matches the goal, then the goal is false.
                // See definition 4.3.2 of [bra2] and section VI-B of [ceri].
                // Substitute the bindings in the rule first.
                // If your rule is `und(X) :- stud(X), not grad(X)` and you're at the `not grad` part, and in the
                // previous goal stud(a) was true, then bindings now contains X:a so we want to search the database
                // for the fact grad(a).
                if (bindings != null)
                {
                    goal = goal.Substitute(bindings.Map);
                }
                foreach (Expr fact in facts.GetIndexed(goal.GetPredicate().GetHashCode()))
                {
                    StackMap<string, string> newBindings = new StackMap<string, string>(bindings);
                    if (fact.Unify(goal, newBindings))
                    {
                        return new System.Collections.Generic.List<IDictionary<string, string>>();
                    }
                }
                // not found
                if (lastGoal)
                {
                    answers.Add(bindings.Map);
                }
                else
                {
                    answers.AddRange(MatchGoals(goals.Skip(1).ToList(), facts, bindings));
                }
            }
            return answers;
        }

        /// <exception cref="Sharplog.DatalogException"/>
        private static int DepthFirstSearch(Expr goal, IEnumerable<Rule> graph, IList<Expr> visited, int level)
        {
            string pred = goal.GetPredicate();
            // Step (1): Guard against negative recursion
            bool negated = goal.IsNegated();
            StringBuilder route = new StringBuilder(pred);
            // for error reporting
            for (int i = visited.Count - 1; i >= 0; i--)
            {
                Expr e = visited[i];
                route.Append(e.IsNegated() ? " <- ~" : " <- ").Append(e.GetPredicate());
                if (e.GetPredicate().Equals(pred))
                {
                    if (negated)
                    {
                        throw new DatalogException("Program is not stratified - predicate " + pred + " has a negative recursion: " + route);
                    }
                    return 0;
                }
                if (e.IsNegated())
                {
                    negated = true;
                }
            }
            visited.Add(goal);
            // Step (2): Do the actual depth-first search to compute the strata
            int m = 0;
            foreach (Rule rule in graph)
            {
                if (rule.GetHead().GetPredicate().Equals(pred))
                {
                    foreach (Expr expr in rule.GetBody())
                    {
                        int x = DepthFirstSearch(expr, graph, visited, level + 1);
                        if (expr.IsNegated())
                        {
                            x++;
                        }
                        if (x > m)
                        {
                            m = x;
                        }
                    }
                }
            }
            visited.RemoveAt(visited.Count - 1);
            return m;
        }

        /* Returns a list of rules that are relevant to the query.
        If for example you're querying employment status, you don't care about family relationships, etc.
        The advantages of this of this optimization becomes bigger the more complex the rules get. */
        /* This basically constructs the dependency graph for semi-naive evaluation: In the returned map, the string
        is a predicate in the rules' heads that maps to a collection of all the rules that have that predicate in
        their body so that we can easily find the rules that are affected when new facts are deduced in different
        iterations of buildDatabase().
        For example if you have a rule p(X) :- q(X) then there will be a mapping from "q" to that rule
        so that when new facts q(Z) are deduced, the rule will be run in the next iteration to deduce p(Z) */
        /* Retrieves all the rules that are affected by a collection of facts.
        * This is used as part of the semi-naive evaluation: When new facts are generated, we
        * take a look at which rules have those facts in their bodies and may cause new facts
        * to be derived during the next iteration.
        * The `dependents` parameter was built earlier in the buildDependentRules() method */
        /* Match the goals in a rule to the facts in the database (recursively).
        * If the goal is a built-in predicate, it is also evaluated here. */

        /// <exception cref="Sharplog.DatalogException"/>
        public IEnumerable<IDictionary<string, string>> Query(Sharplog.Jatalog jatalog, IList<Expr> goals, StackMap<string, string> bindings)
        {
            if ((goals.Count == 0))
            {
                return new System.Collections.Generic.List<IDictionary<string, string>>();
            }
            // Reorganize the goals so that negated literals are at the end.
            IList<Expr> orderedGoals = Sharplog.Engine.Engine.ReorderQuery(goals);
            IEnumerable<string> predicates = GetRelevantPredicates(jatalog, goals);
            IEnumerable<Rule> rules = new HashSet<Rule>(jatalog.GetIdb().Where((Rule rule) => predicates.Contains(rule.GetHead().GetPredicate())));
            // Build an IndexedSet<> with only the relevant facts for this particular query.
            IndexedSet<Expr> facts = new IndexedSet<Expr>();
            foreach (string predicate in predicates)
            {
                facts.AddAll(jatalog.GetEdbProvider().GetFacts(predicate));
            }
            // Build the database. A Set ensures that the facts are unique
            IndexedSet<Expr> resultSet = ExpandDatabase(facts, rules);
            // Now match the expanded database to the goals
            return MatchGoals(orderedGoals, resultSet, bindings);
        }

        /* The core of the bottom-up implementation:
        * It computes the stratification of the rules in the EDB and then expands each
        * strata in turn, returning a collection of newly derived facts. */

        /// <exception cref="Sharplog.DatalogException"/>
        private IndexedSet<Expr> ExpandDatabase(IndexedSet<Expr> facts, IEnumerable<Rule> allRules)
        {
            IList<IList<Rule>> strata = ComputeStratification(allRules.ToList());
            for (int i = 0; i < strata.Count; i++)
            {
                IEnumerable<Rule> rules = strata[i];
                ExpandStrata(facts, rules);
            }
            return facts;
        }

        /* This implements the semi-naive part of the evaluator.
        * For all the rules derive a collection of new facts; Repeat until no new
        * facts can be derived.
        * The semi-naive part is to only use the rules that are affected by newly derived
        * facts in each iteration of the loop.
        */

        private IEnumerable<Expr> ExpandStrata(IndexedSet<Expr> facts, IEnumerable<Rule> strataRules)
        {
            if (strataRules == null || (strataRules.Count() == 0))
            {
                return new System.Collections.Generic.List<Expr>();
            }
            IEnumerable<Rule> rules = strataRules;
            IDictionary<int, List<Rule>> dependentRules = BuildDependentRules(strataRules);
            while (true)
            {
                // Match each rule to the facts
                IndexedSet<Expr> newFacts = new IndexedSet<Expr>();
                foreach (Rule rule in rules)
                {
                    newFacts.AddAll(MatchRule(facts, rule));
                }
                // Repeat until there are no more facts added
                if ((newFacts.Count == 0))
                {
                    return facts;
                }
                // Determine which rules depend on the newly derived facts
                rules = GetDependentRules(newFacts, dependentRules);
                facts.AddAll(newFacts);
            }
        }

        /* Match the facts in the EDB against a specific rule */

        private HashSet<Expr> MatchRule(IndexedSet<Expr> facts, Rule rule)
        {
            if ((rule.GetBody().Count == 0))
            {
                // If this happens, you're using the API wrong.
                return new System.Collections.Generic.HashSet<Expr>();
            }
            // Match the rule body to the facts.
            IEnumerable<IDictionary<string, string>> answers = MatchGoals(rule.GetBody(), facts, null);
            return new HashSet<Expr>(answers.Select((IDictionary<string, string> answer) => rule.GetHead().Substitute(answer)).Where((Expr derivedFact) => !facts.Contains(derivedFact)));
        }
    }
}