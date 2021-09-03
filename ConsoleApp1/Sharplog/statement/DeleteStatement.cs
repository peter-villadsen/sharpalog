using System.Collections.Generic;
using Sharplog.Engine;

namespace Sharplog.Statement
{
    internal class DeleteStatement : Sharplog.Statement.Statement
    {
        private List<Expr> goals;

        internal DeleteStatement(List<Expr> goals)
        {
            this.goals = goals;
        }

        /// <exception cref="Sharplog.DatalogException"/>
        public IEnumerable<StackMap> Execute(Sharplog.Jatalog datalog, StackMap bindings)
        {
            datalog.Delete(goals, bindings);
            return null;
        }
    }
}