using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities.Attributes;

namespace SLT.Services._Order.DTOs.Updates
{
    public class InvoiceIdUpdate
    {
       [StringInputValidation(maxLength:32,minLength:(32))] public string InvoiceId { get; set; }
    }
}
