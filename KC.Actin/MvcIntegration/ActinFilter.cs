using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin {
    public class ActinFilter : IActionFilter {

        private string m_directorName;
        public ActinFilter(string directorName = null) {
            this.m_directorName = directorName;
        }

        public void OnActionExecuting(ActionExecutingContext context) {
            if (Director.TryGetDirector(m_directorName ?? "", out var director)) {
                director.WithExternal_ResolveDependencies(context.Controller);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) {
            if (Director.TryGetDirector(m_directorName ?? "", out var director)) {
                director.WithExternal_DisposeChildren(context.Controller);
            }
        }

    }
}
