using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Twilio.TwiML;
using Twilio.AspNet.Mvc;
using MySql.Data.MySqlClient;
using System.Diagnostics;
using Microsoft.Ajax.Utilities;
using Hanssens.Net;
using System.Web.WebPages;
using System.Text.RegularExpressions;

namespace TwilioCallForwarder.Controllers
{
    public class CongressController : TwilioController
    {
        string cs = @"server=localhost;userid=root;password=root;database=twilio_forward";

        //Dictionary<string, int> _tourManagerDict = new Dictionary<string, int>();

        public const int RETRY_COUNT = 3;
        public const string EMERGENCY_NUMBER = "07872618543";

        // GET: Congress
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Welcome(string callSid, string from, string to, string callStatus)
        {
            var voiceResponse = new VoiceResponse();

            Debug.WriteLine("Welcome : callSid = " + callSid + " from = " + from + " to = " + to + " callStatus = " + callStatus);

            voiceResponse.Say("Thank You for calling Newmarket Holidays, Please enter tour manager I D");
            voiceResponse.Gather(numDigits: 6, action: new Uri($"/congress/tourLookup/", UriKind.Relative), method: "POST");

            return TwiML(voiceResponse);
        }

        [AcceptVerbs("GET", "POST")]
        public ActionResult TourLookup(int digits, string callSid, string from, string to, string callStatus)
        {
            string tourMobileNumber = null;

			Debug.WriteLine("TourLookup : callSid = " + callSid + " from = " + from + " to = " + to + " callStatus = " + callStatus);

            var storage = new LocalStorage();

            storage.Load();

            if (!storage.Exists(callSid))
            {
                storage.Store(callSid, RETRY_COUNT);
			} 

			using var con = new MySqlConnection(cs);
            con.Open();

            Debug.WriteLine("TourLookup {0}", digits);

            string sql = "SELECT * FROM tourmanagers WHERE tourManagerID = " + digits;
            using var cmd = new MySqlCommand(sql, con);

            using MySqlDataReader rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                if (rdr.IsDBNull(5))
                {
                    tourMobileNumber = "";
                } else
                {
                    if (!string.IsNullOrEmpty(rdr.GetString(5)))
                        tourMobileNumber = rdr.GetString(5);
                    else
                        tourMobileNumber = "";
                }
                break;
            }

            rdr.Close();

            if (tourMobileNumber == null)
            {
                int sid = storage.Get<int>(callSid);
                sid = sid - 1;
                storage.Store(callSid, sid);
                storage.Persist();
				return RedirectToAction("noTourManager", new { curCallSid = callSid });
            } else if (tourMobileNumber.Equals(""))
            {
                return RedirectToAction("callEmergency");
            }
            
            if (!IsPhoneNumber(tourMobileNumber))
            {
                return RedirectToAction("goodBye");
            }

            storage.Remove(callSid);
            storage.Persist();
            return RedirectToAction("callTourManager", new { tourManagerNumber = tourMobileNumber });
        }

        public bool IsPhoneNumber(string number)
        {
            return Regex.Match(number, @"^(((\+44\s?\d{4}|\(?0\d{4}\)?)\s?\d{3}\s?\d{3})|((\+44\s?\d{3}|\(?0\d{3}\)?)\s?\d{3}\s?\d{4})|((\+44\s?\d{2}|\(?0\d{2}\)?)\s?\d{4}\s?\d{4}))(\s?\#(\d{4}|\d{3}))?$").Success;
        }

        [AcceptVerbs("GET", "POST")]
        public ActionResult CallTourManager(string tourManagerNumber)
        {
            var voiceResponse = new VoiceResponse();
            Debug.WriteLine("Forwarding to " + tourManagerNumber);
            var sayMessage = $"We are now transferring your call to tour manager who can help your further.";

            voiceResponse.Say(sayMessage);
            voiceResponse.Dial(number: tourManagerNumber, null);

            return TwiML(voiceResponse);
        }

        [AcceptVerbs("GET", "POST")]
        public ActionResult NoTourManager(string curCallSid)
        {
            Debug.WriteLine("NoTourManager : callSid = " + curCallSid);
            var storage = new LocalStorage();
            storage.Load();

            var voiceResponse = new VoiceResponse();

            int remainAttempt = storage.Get<int>(curCallSid);
            if (remainAttempt == 0)
            {
				return RedirectToAction("callEmergency");
			}
			var sayMessage = $"Sorry, there is no tour manager for that id. Please input valid id. You have {remainAttempt} attempt.";
			voiceResponse.Say(sayMessage);
			voiceResponse.Gather(numDigits: 6, action: new Uri($"/congress/tourLookup/", UriKind.Relative), method: "POST");

			return TwiML(voiceResponse);
        }

        [AcceptVerbs("GET", "POST")]
        public ActionResult Goodbye()
        {
            var voiceResponse = new VoiceResponse();
            /*voiceResponse.Say("Thank you for using Call Congress! " +
                "Your voice makes a difference. Goodbye.");*/
            voiceResponse.Hangup();

            return TwiML(voiceResponse);
        }

		[AcceptVerbs("GET", "POST")]
		public ActionResult CallEmergency()
		{
			var voiceResponse = new VoiceResponse();
			Debug.WriteLine("Call Emergency");
			var sayMessage = $"We are now transferring your call to help your further";

			voiceResponse.Say(sayMessage);
			voiceResponse.Dial(number: EMERGENCY_NUMBER, null);

			return TwiML(voiceResponse);
		}

		[HttpPost]
		public ActionResult CallStatusCallback(string callSid, string from, string to, string callStatus)
		{
			Debug.WriteLine("CallStatusCallback : callSid = " + callSid + " from = " + from + " to = " + to + " callStatus = " + callStatus);

            return null;
		}
	}
}