using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            if (Session["myval"] != null)
            {
                var va = Session["myval"];
                Session["myval2"] = (string)Session["myval2"] + (int)Session["myval3"];
                Session["myval3"] = (int)Session["myval3"] + 1;
                Response.Write(va);
                Response.Write(Session["myval2"]);
            }
            else
            {
                Session["myval"] = "My Session values ";
                Session["myval2"] = "20";
                Session["myval3"] = 30;
            }

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}