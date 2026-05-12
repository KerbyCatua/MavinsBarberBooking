using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace MavinsBarberBooking.Controllers
{
    // api/webhook/paymongo
    [Route("api/[controller]")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly IDbConnection _db;
        private readonly IConfiguration _config;

        public WebhookController(IDbConnection db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        [HttpPost("paymongo")]
        public async Task<IActionResult> PayMongoWebhook()
        {
            try
            {
                // 1. Read Raw JSON Body requested by PayMongo
                using var reader = new StreamReader(Request.Body);
                var rawBody = await reader.ReadToEndAsync();

                // 2. Security Validation (ENSURES IT CAME FROM PAYMONGO, NOT HACKERS)
                var signatureHeader = Request.Headers["paymongo-signature"].ToString();
                if (string.IsNullOrEmpty(signatureHeader)) return Unauthorized("Missing Signature");

                var webhookSecret = _config["PayMongo:WebhookSecret"];
                if (string.IsNullOrEmpty(webhookSecret)) return Unauthorized("Missing Webhook Secret");

                var signatureParts = signatureHeader.Split(',').Select(part => part.Split('=')).ToDictionary(split => split[0], split => split[1]);
                var timestamp = signatureParts["t"];
                var expectedSignature = signatureParts.ContainsKey("te") ? signatureParts["te"] : signatureParts["li"];

                var signaturePayload = timestamp + "." + rawBody;
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
                var computedHash = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signaturePayload))).Replace("-", "").ToLower();

                if (computedHash != expectedSignature) return Unauthorized("Invalid Signature");

                // 3. Process the Payment
                using var jsonDocument = JsonDocument.Parse(rawBody);
                var eventType = jsonDocument.RootElement.GetProperty("data").GetProperty("attributes").GetProperty("type").GetString();

                // 4. Update the Database using Dapper
                if (eventType == "checkout_session.payment.paid")
                {
                    var attributes = jsonDocument.RootElement.GetProperty("data").GetProperty("attributes").GetProperty("data").GetProperty("attributes");

                    // Fetch the booking ID from the reference_number we set in the payload
                    var bookingIdStr = attributes.GetProperty("reference_number").GetString();

                    if (int.TryParse(bookingIdStr, out int bookingId))
                    {
                        await _db.ExecuteAsync("UPDATE Payments SET PaymentStatus = 1 WHERE BookingId = @BookingId", new { BookingId = bookingId });
                        await _db.ExecuteAsync("UPDATE Bookings SET Status = 'Upcoming' WHERE BookingId = @BookingId", new { BookingId = bookingId });
                    }
                }

                return Ok(); // Tells PayMongo "We got it successfully"
            }
            catch (Exception ex)
            {
                Console.WriteLine("Webhook Error: " + ex.Message);
                return BadRequest();
            }
        }
    }
}