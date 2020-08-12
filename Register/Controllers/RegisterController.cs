using Register.DBContent;
using Register.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using WebMatrix.WebData;
using System.Data.Entity;
using Microsoft.Ajax.Utilities;
using System.Data.SqlClient;
using System.Data;

namespace Register.Controllers
{
    public class RegisterController : Controller
    {
        string cnnString = System.Configuration.ConfigurationManager.ConnectionStrings["DBModel1"].ConnectionString;
        DBModel dB = new DBModel();
        string changemail;
        // GET: Register
        [ActionName("Index")]
        public ActionResult Index1(UserM objUsr)
        {
            return View();
        }
        [HttpPost]
        public ActionResult Index(UserM objUsr)
        {
            // email not verified on registration time  
            objUsr.EmailVerification = false;
            var IsExists = IsEmailExists(objUsr.Email);
            if(IsExists)
            {
                ModelState.AddModelError("EmailExists", "Email Already Exists");
                return View();
            }
            //it generate unique code     
            objUsr.ActivetionCode = Guid.NewGuid();
            //password convert  
            objUsr.Password = Register.Models.EncryptPassword.textToEncrypt(objUsr.Password);
            dB.UserMs.Add(objUsr);
            dB.SaveChanges();

            #region Send E-Mail Verification Link
            SendEmailToUser(objUsr.Email, objUsr.ActivetionCode.ToString());
            var Message = "Registration Completed, Please Check your Email :" + objUsr.Email;
            ViewBag.Message = Message;
            #endregion
            return View("Registration");
        }
        public bool IsEmailExists(string eMail)
        {
            var IsCheck = dB.UserMs.Where(email => email.Email == eMail).FirstOrDefault();
            return IsCheck != null;
        }
        public bool IsOTPExists(string otp)
        {

            var IsCheck = dB.UserMs.Where(x => x.OTP == otp).FirstOrDefault();
            return IsCheck != null;
        }

        public void SendEmailToUser(string emailId, string activationCode)
        {
            var GenarateUserVerificationLink = "/Register/UserVerification/" + activationCode;
            var link = Request.Url.AbsoluteUri.Replace(Request.Url.PathAndQuery, GenarateUserVerificationLink);

            var fromMail = new MailAddress("jayshripatil1395@gmail.com", "Jayshri"); // set your email  
            var fromEmailpassword = "manisha@13"; // Set your password   
            var toEmail = new MailAddress(emailId);

            var smtp = new SmtpClient();
            smtp.Host = "smtp.gmail.com";
            smtp.Port = 587;
            smtp.EnableSsl = true;
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(fromMail.Address, fromEmailpassword);

            var Message = new MailMessage(fromMail, toEmail);
            Message.Subject = "Registration Completed-Demo";
            Message.Body = "<br/> Your registration completed succesfully." +
                           "<br/> please click on the below link for account verification" +
                           "<br/><br/><a href=" + link + ">" + link + "</a>";
            Message.IsBodyHtml = true;
            smtp.Send(Message);
        }

        #region Verification from Email Account.  
        public ActionResult UserVerification(string id)
        {

            bool Status = false;

            dB.Configuration.ValidateOnSaveEnabled = false; // Ignor to password confirmation   
            var IsVerify = dB.UserMs.Where(u => u.ActivetionCode == new Guid(id)).FirstOrDefault();

            if (IsVerify != null)
            {
                IsVerify.EmailVerification = true;
                dB.SaveChanges();
                ViewBag.Message = "Email Verification completed";
                Status = true;
            }
            else
            {
                ViewBag.Message = "Invalid Request...Email not verify";
                ViewBag.Status = false;
            }

            return View();
        }
        #endregion

        #region User login
        //Use this method generate login view
        public ActionResult Login()
        {
            return View();
        }
        #endregion
        [HttpGet]
        public ActionResult ForgetPassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ForgetPassword(ForgetPassword pass)
        {
            var IsExists = IsEmailExists(pass.EmailId);
            if (!IsExists)
            {
                ModelState.AddModelError("EmailNotExists", "This email is not exists");
                return View();
            }

            //HttpContext.Session.SetString("emailid", pass.EmailId.ToString());
            
            changemail = pass.EmailId;
            var objUsr = dB.UserMs.Where(x => x.Email == pass.EmailId).FirstOrDefault();

            // Genrate OTP   
            string OTP = GeneratePassword();

            objUsr.ActivetionCode = Guid.NewGuid();
            objUsr.OTP = OTP;
            dB.Entry(objUsr).State = System.Data.Entity.EntityState.Modified;
            dB.SaveChanges();

            ForgetPasswordEmailToUser(objUsr.Email, objUsr.ActivetionCode.ToString(), objUsr.OTP);
            return RedirectToAction("ChangePassword", "Register",new { EmailId = pass.EmailId});
        }

        [Authorize]
        public ActionResult LogOut()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "Register");
        }
        [HttpPost]
        public ActionResult Login(UserLogin LgnUsr)
        {
            var _passWord = Register.Models.EncryptPassword.textToEncrypt(LgnUsr.Password);
            bool Isvalid = dB.UserMs.Any(x => x.Email == LgnUsr.EmailId && x.EmailVerification == true &&
            x.Password == _passWord);
            if (Isvalid)
            {
                int timeout = LgnUsr.Rememberme ? 60 : 5; // Timeout in minutes, 60 = 1 hour.  
                var ticket = new FormsAuthenticationTicket(LgnUsr.EmailId, false, timeout);
                string encrypted = FormsAuthentication.Encrypt(ticket);
                var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted);
                cookie.Expires = System.DateTime.Now.AddMinutes(timeout);
                cookie.HttpOnly = true;
                Response.Cookies.Add(cookie);
                return RedirectToAction("Index", "UserDash");
            }
            else
            {
                ModelState.AddModelError("", "Invalid Information... Please try again!");
            }
            return View();
        }
        public string GeneratePassword()
        {
            string OTPLength = "4";
            string OTP = string.Empty;

            string Chars = string.Empty;
            Chars = "1,2,3,4,5,6,7,8,9,0";

            char[] seplitChar = { ',' };
            string[] arr = Chars.Split(seplitChar);
            string NewOTP = "";
            string temp = "";
            Random rand = new Random();
            for (int i = 0; i < Convert.ToInt32(OTPLength); i++)
            {
                temp = arr[rand.Next(0, arr.Length)];
                NewOTP += temp;
                OTP = NewOTP;
            }
            return OTP;
        }

        public void ForgetPasswordEmailToUser(string emailId, string activationCode,string OTP)
        {
            var GenarateUserVerificationLink = "/Register/ChangePassword/" + activationCode;
            var link = Request.Url.AbsoluteUri.Replace(Request.Url.PathAndQuery, GenarateUserVerificationLink);

            var fromMail = new MailAddress("jayshripatil1395@gmail.com", "Jayshri"); // set your email  
            var fromEmailpassword = "manisha@13"; // Set your password   
            var toEmail = new MailAddress(emailId);

            var smtp = new SmtpClient();
            smtp.Host = "smtp.gmail.com";
            smtp.Port = 587;
            smtp.EnableSsl = true;
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(fromMail.Address, fromEmailpassword);

            var Message = new MailMessage(fromMail, toEmail);
            Message.Subject = "Password Reset Demo";
            Message.Body = "<br/> Please click on the below link for password change." +
                           "<br/><br/><a href=" + link + ">" + link + "</a>" +
                           "<br/> OTP for Password Change :" + OTP;
                           ;
            Message.IsBodyHtml = true;
            smtp.Send(Message);
        }

        public ActionResult ChangePassword(ForgetPassword forgetPassword)
        {
            changemail = forgetPassword.EmailId;
            return View();
        }

        [HttpPost]
        public ActionResult ChangePassword(UserM objUsr, UserLogin LgnUsr,ChangePassword changeP,ForgetPassword pass)
        {
            var IsExists = IsEmailExists(pass.EmailId);
            if (!IsExists)
            {
                var IsOTP = IsOTPExists(changeP.OTP);
                if (!IsOTP)
                {
                    ModelState.AddModelError("OTPNotExists", "This OTP is not exists");
                    return View();
                }
            }
            var _Password = Register.Models.EncryptPassword.textToEncrypt(changeP.Password);
            SqlConnection conn = new SqlConnection(cnnString);
            conn.Open();
            SqlCommand dCmd = new SqlCommand("sp_ChangePass", conn);
            dCmd.CommandType = CommandType.StoredProcedure;
            dCmd.Parameters.AddWithValue("@changep", _Password);
            dCmd.Parameters.AddWithValue("@Email", changeP.EmailId);
            dCmd.ExecuteNonQuery();
            conn.Close();
            //var _Password = Register.Models.EncryptPassword.textToEncrypt(changeP.Password);
            //var userdb = dB.UserMs.FirstOrDefault(x => x.Email == pass.EmailId);
            ////dB.UserMs.Add(objUsr);
            //dB.SaveChanges();

            ////var userdb = dB.UserMs.FirstOrDefault(x => x.Password == changeP.Password);
            ////var _passWord = Register.Models.EncryptPassword.textToEncrypt(changeP.Password);
            ////userdb.Password = changeP.Password;
            ////dB.SaveChanges();
            return RedirectToAction("Login", "Register");
        }
        
    }
}