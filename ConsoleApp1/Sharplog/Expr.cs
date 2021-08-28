using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sharpen;
using Sharplog.Engine;

namespace Sharplog
{
    /// <summary>Represents a Datalog literal expression.</summary>
    /// <remarks>
    /// Represents a Datalog literal expression.
    /// <p>
    /// An expression is a predicate followed by zero or more terms, in the form
    /// <c>pred(term1, term2, term3...)</c>
    /// .
    /// </p><p>
    /// An expression is said to be <i>ground</i> if it contains no <i>variables</i> in its terms. Variables are indicated in
    /// terms starting with an upper-case letter, for example, the term A in
    /// <c>ancestor(A, bob)</c>
    /// is a variable while the term "bob" is
    /// not.
    /// </p><p>
    /// The number of terms is the expression's <i>arity</i>.
    /// </p>
    /// </remarks>
    public class Expr : Indexable
    {
        protected internal bool negated = false;
        private string predicate;

        private IList<string> terms;

        /// <summary>Standard constructor that accepts a predicate and a list of terms.</summary>
        /// <param name="predicate">The predicate of the expression.</param>
        /// <param name="terms">The terms of the expression.</param>
        public Expr(string predicate, IList<string> terms)
        {
            this.predicate = predicate;
            // I've seen both versions of the symbol for not equals being used, so I allow
            // both, but we convert to "<>" internally to simplify matters later.
            if (this.predicate.Equals("!="))
            {
                this.predicate = "<>";
            }
            this.terms = terms;
        }

        /// <summary>Constructor for the fluent API that allows a variable number of terms.</summary>
        /// <param name="predicate">The predicate of the expression.</param>
        /// <param name="terms">The terms of the expression.</param>
        public Expr(string predicate, params string[] terms)
            : this(predicate, terms.ToList())
        {
        }

        /// <summary>Helper method for creating a new expression.</summary>
        /// <remarks>
        /// Helper method for creating a new expression.
        /// This method is part of the fluent API intended for
        /// <c>import static</c>
        /// </remarks>
        /// <param name="predicate">The predicate of the expression.</param>
        /// <param name="terms">The terms of the expression.</param>
        /// <returns>the new expression</returns>
        public static Sharplog.Expr CreateExpr(string predicate, params string[] terms)
        {
            return new Sharplog.Expr(predicate, terms);
        }

        /// <summary>Static method for constructing negated expressions in the fluent API.</summary>
        /// <remarks>
        /// Static method for constructing negated expressions in the fluent API.
        /// Negated expressions are of the form
        /// <c>not predicate(term1, term2,...)</c>
        /// .
        /// </remarks>
        /// <param name="predicate">The predicate of the expression</param>
        /// <param name="terms">The terms of the expression</param>
        /// <returns>The negated expression</returns>
        public static Sharplog.Expr Not(string predicate, params string[] terms)
        {
            Sharplog.Expr e = new Sharplog.Expr(predicate, terms);
            e.negated = true;
            return e;
        }

        /// <summary>
        /// Static helper method for constructing an expression
        /// <c>a = b</c>
        /// in the fluent API.
        /// </summary>
        /// <param name="a">the left hand side of the operator</param>
        /// <param name="b">the right hand side of the operator</param>
        /// <returns>the expression</returns>
        public static Sharplog.Expr Eq(string a, string b)
        {
            return new Sharplog.Expr("=", a, b);
        }

        /// <summary>
        /// Static helper method for constructing an expression
        /// <c>a &lt;&gt; b</c>
        /// in the fluent API.
        /// </summary>
        /// <param name="a">the left hand side of the operator</param>
        /// <param name="b">the right hand side of the operator</param>
        /// <returns>the expression</returns>
        public static Sharplog.Expr Ne(string a, string b)
        {
            return new Sharplog.Expr("<>", a, b);
        }

        /// <summary>
        /// Static helper method for constructing an expression
        /// <c>a &lt; b</c>
        /// in the fluent API.
        /// </summary>
        /// <param name="a">the left hand side of the operator</param>
        /// <param name="b">the right hand side of the operator</param>
        /// <returns>the expression</returns>
        public static Sharplog.Expr Lt(string a, string b)
        {
            return new Sharplog.Expr("<", a, b);
        }

        /// <summary>
        /// Static helper method for constructing an expression
        /// <c>a &lt;= b</c>
        /// in the fluent API.
        /// </summary>
        /// <param name="a">the left hand side of the operator</param>
        /// <param name="b">the right hand side of the operator</param>
        /// <returns>the expression</returns>
        public static Sharplog.Expr Le(string a, string b)
        {
            return new Sharplog.Expr("<=", a, b);
        }

        /// <summary>
        /// Static helper method for constructing an expression
        /// <c>a &gt; b</c>
        /// in the fluent API.
        /// </summary>
        /// <param name="a">the left hand side of the operator</param>
        /// <param name="b">the right hand side of the operator</param>
        /// <returns>the expression</returns>
        public static Sharplog.Expr Gt(string a, string b)
        {
            return new Sharplog.Expr(">", a, b);
        }

        /// <summary>
        /// Static helper method for constructing an expression
        /// <c>a &gt;= b</c>
        /// in the fluent API.
        /// </summary>
        /// <param name="a">the left hand side of the operator</param>
        /// <param name="b">the right hand side of the operator</param>
        /// <returns>the expression</returns>
        public static Sharplog.Expr Ge(string a, string b)
        {
            return new Sharplog.Expr(">=", a, b);
        }

        /// <summary>The arity of an expression is simply the number of terms.</summary>
        /// <remarks>
        /// The arity of an expression is simply the number of terms.
        /// For example, an expression
        /// <c>foo(bar, baz, fred)</c>
        /// has an arity of 3 and is sometimes
        /// written as
        /// <c>foo/3</c>
        /// .
        /// It is expected that the arity of facts with the same predicate is the same, although Jatalog
        /// does not enforce it (expressions with the same predicates but different arities wont unify).
        /// </remarks>
        /// <returns>the arity</returns>
        public virtual int Arity()
        {
            return terms.Count;
        }

        /// <summary>An expression is said to be ground if none of its terms are variables.</summary>
        /// <returns>true if the expression is ground</returns>
        public virtual bool IsGround()
        {
            foreach (string term in terms)
            {
                if (Sharplog.Jatalog.IsVariable(term))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Checks whether the expression is negated, eg.</summary>
        /// <remarks>
        /// Checks whether the expression is negated, eg.
        /// <c>not foo(bar, baz)</c>
        /// </remarks>
        /// <returns>true if the expression is negated</returns>
        public virtual bool IsNegated()
        {
            return negated;
        }

        /// <summary>Checks whether an expression represents one of the supported built-in predicates.</summary>
        /// <remarks>
        /// Checks whether an expression represents one of the supported built-in predicates.
        /// Jatalog supports several built-in operators: =, &lt;&gt;, &lt;, &lt;=, &gt;, &gt;=.
        /// These are represented internally as expressions with the operator in the predicate and the operands
        /// in the terms. Thus, a clause like
        /// <c>X &gt; 100</c>
        /// is represented internally as
        /// <c>"&gt;"(X, 100)</c>
        /// .
        /// If the engine encounters one of these predicates it calls
        /// <see cref="EvalBuiltIn(System.Collections.Generic.IDictionary{K, V})"/>
        /// rather than unifying
        /// it against the goals.
        /// </remarks>
        /// <returns>true if the expression is a built-in predicate.</returns>
        public virtual bool IsBuiltIn()
        {
            char op = predicate[0];
            return !char.IsLetterOrDigit(op) && op != '\"';
        }

        /// <summary>
        /// Unifies
        /// <c>this</c>
        /// expression with another expression.
        /// </summary>
        /// <param name="that">The expression to unify with</param>
        /// <param name="bindings">The bindings of variables to values after unification</param>
        /// <returns>true if the expressions unify.</returns>
        public virtual bool Unify(Sharplog.Expr that, StackMap<string, string> bindings)
        {
            if (!this.predicate.Equals(that.predicate) || this.Arity() != that.Arity())
            {
                return false;
            }
            for (int i = 0; i < this.Arity(); i++)
            {
                string term1 = this.terms[i];
                string term2 = that.terms[i];
                if (Sharplog.Jatalog.IsVariable(term1))
                {
                    if (!term1.Equals(term2))
                    {
                        if (!bindings.ContainsKey(term1))
                        {
                            bindings.Add(term1, term2);
                        }
                        else if (!bindings.Get(term1).Equals(term2))
                        {
                            return false;
                        }
                    }
                }
                else if (Sharplog.Jatalog.IsVariable(term2))
                {
                    if (!bindings.ContainsKey(term2))
                    {
                        bindings.Add(term2, term1);
                    }
                    else if (!bindings.Get(term2).Equals(term1))
                    {
                        return false;
                    }
                }
                else if (!term1.Equals(term2))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Substitutes the variables in this expression with bindings from a unification.</summary>
        /// <param name="bindings">The bindings to substitute.</param>
        /// <returns>A new expression with the variables replaced with the values in bindings.</returns>
        public virtual Sharplog.Expr Substitute(IDictionary<string, string> bindings)
        {
            // that.terms.add() below doesn't work without the new ArrayList()
            Sharplog.Expr that = new Sharplog.Expr(this.predicate, new List<string>());
            that.negated = negated;
            foreach (string term in this.terms)
            {
                string value;
                if (Sharplog.Jatalog.IsVariable(term))
                {
                    value = bindings.GetOrNull(term);
                    if (value == null)
                    {
                        value = term;
                    }
                }
                else
                {
                    value = term;
                }
                that.terms.Add(value);
            }
            return that;
        }

        /// <summary>Evaluates a built-in predicate.</summary>
        /// <param name="bindings">A map of variable bindings</param>
        /// <returns>true if the operator matched.</returns>
        public virtual bool EvalBuiltIn(StackMap<string, string> bindings)
        {
            // This method may throw a RuntimeException for a variety of possible reasons, but
            // these conditions are supposed to have been caught earlier in the chain by
            // methods such as Rule#validate().
            // The RuntimeException is a requirement of using the Streams API.
            string term1 = terms[0];
            if (Sharplog.Jatalog.IsVariable(term1) && bindings.ContainsKey(term1))
            {
                term1 = bindings.Get(term1);
            }
            string term2 = terms[1];
            if (Sharplog.Jatalog.IsVariable(term2) && bindings.ContainsKey(term2))
            {
                term2 = bindings.Get(term2);
            }
            if (predicate.Equals("="))
            {
                // '=' is special
                if (Sharplog.Jatalog.IsVariable(term1))
                {
                    if (Sharplog.Jatalog.IsVariable(term2))
                    {
                        // Rule#validate() was supposed to catch this condition
                        throw new InvalidOperationException("Both operands of '=' are unbound (" + term1 + ", " + term2 + ") in evaluation of " + this);
                    }

                    bindings.Add(term1, term2);

                    return true;
                }
                else if (Sharplog.Jatalog.IsVariable(term2))
                {
                    bindings.Add(term2, term1);
                    return true;
                }
                else if (Parser.TryParseDouble(term1) && Parser.TryParseDouble(term2))
                {
                    double d1 = double.Parse(term1);
                    double d2 = double.Parse(term2);
                    return d1 == d2;
                }
                else
                {
                    return term1.Equals(term2);
                }
            }
            else
            {
                try
                {
                    // These errors can be detected in the validate method:
                    if (Sharplog.Jatalog.IsVariable(term1) || Sharplog.Jatalog.IsVariable(term2))
                    {
                        // Rule#validate() was supposed to catch this condition
                        throw new InvalidOperationException("Unbound variable in evaluation of " + this);
                    }
                    if (predicate.Equals("<>"))
                    {
                        // '<>' is also a bit special
                        if (Parser.TryParseDouble(term1) && Parser.TryParseDouble(term2))
                        {
                            double d1 = double.Parse(term1);
                            double d2 = double.Parse(term2);
                            return d1 != d2;
                        }
                        else
                        {
                            return !term1.Equals(term2);
                        }
                    }
                    else
                    {
                        // Ordinary comparison operator
                        // If the term doesn't parse to a double it gets treated as 0.0.
                        double d1 = 0.0;
                        double d2 = 0.0;
                        if (Parser.TryParseDouble(term1))
                        {
                            d1 = double.Parse(term1);
                        }
                        if (Parser.TryParseDouble(term2))
                        {
                            d2 = double.Parse(term2);
                        }
                        switch (predicate)
                        {
                            case "<":
                                {
                                    return d1 < d2;
                                }

                            case "<=":
                                {
                                    return d1 <= d2;
                                }

                            case ">":
                                {
                                    return d1 > d2;
                                }

                            case ">=":
                                {
                                    return d1 >= d2;
                                }
                        }
                    }
                }
                catch (FormatException e)
                {
                    // You found a way to write a double in a way that the regex in tryParseDouble() doesn't understand.
                    throw new InvalidOperationException("tryParseDouble() experienced a false positive!?", e);
                }
            }
            throw new InvalidOperationException("Unimplemented built-in predicate " + predicate);
        }

        public virtual string GetPredicate()
        {
            return predicate;
        }

        public virtual IList<string> GetTerms()
        {
            return terms;
        }

        public override bool Equals(object other)
        {
            if (other == null || !(other is Sharplog.Expr))
            {
                return false;
            }
            Sharplog.Expr that = ((Sharplog.Expr)other);
            if (!this.predicate.Equals(that.predicate))
            {
                return false;
            }
            if (Arity() != that.Arity() || negated != that.negated)
            {
                return false;
            }
            for (int i = 0; i < terms.Count; i++)
            {
                if (!terms[i].Equals(that.terms[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            int hash = predicate.GetHashCode();
            foreach (string term in terms)
            {
                hash += term.GetHashCode();
            }
            return hash;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (IsNegated())
            {
                sb.Append("not ");
            }
            if (IsBuiltIn())
            {
                TermToString(sb, terms[0]);
                sb.Append(" ").Append(predicate).Append(" ");
                TermToString(sb, terms[1]);
            }
            else
            {
                sb.Append(predicate).Append('(');
                for (int i = 0; i < terms.Count; i++)
                {
                    string term = terms[i];
                    TermToString(sb, term);
                    if (i < terms.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }
                sb.Append(')');
            }
            return sb.ToString();
        }

        /* Converts a term to a string. If it started as a quoted string it is now enclosed in quotes,
        * and other quotes escaped.
        * caveat: You're going to have trouble if you have other special characters in your strings */

        public int Index()
        {
            return predicate.GetHashCode();
        }

        /// <summary>Validates a fact in the IDB.</summary>
        /// <remarks>
        /// Validates a fact in the IDB.
        /// Valid facts must be ground and cannot be negative.
        /// </remarks>
        /// <exception cref="DatalogException">if the fact is invalid.</exception>
        /// <exception cref="Sharplog.DatalogException"/>
        public virtual void ValidFact()
        {
            if (!IsGround())
            {
                throw new DatalogException("Fact " + this + " is not ground");
            }
            else if (IsNegated())
            {
                throw new DatalogException("Fact " + this + " is negated");
            }
        }

        private static StringBuilder TermToString(StringBuilder sb, string term)
        {
            if (term.StartsWith("\""))
            {
                sb.Append('"').Append(term.Substring(1).Replace("\"", "\\\\\"")).Append('"');
            }
            else
            {
                sb.Append(term);
            }
            return sb;
        }
    }
}