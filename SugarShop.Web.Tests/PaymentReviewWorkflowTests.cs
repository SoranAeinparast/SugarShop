using SugarShop.Domain.Entities.Sales;
using SugarShop.Web.Extensions;
using SugarShop.Web.Tests.Infrastructure;
using Xunit;

namespace SugarShop.Web.Tests;

public class PaymentReviewWorkflowTests
{
    [Fact]
    public void Refunded_payment_has_a_user_facing_label()
    {
        Assert.Equal("مسترد شده", PaymentStatus.Refunded.ToFarsi());
    }

    [Fact]
    public void Payment_review_queue_includes_internal_recharges_and_resolution_is_audited()
    {
        var admin = File.ReadAllText(Path.Combine(RepositoryLayout.WebProjectDir, "Controllers", "AdminController.cs"));
        var payments = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "SugarShop.Domain", "Entities", "Sales", "Payment.cs"));
        var queueStart = admin.IndexOf("public async Task<IActionResult> PaymentReviews(", StringComparison.Ordinal);
        Assert.True(queueStart >= 0);
        var queueEnd = admin.IndexOf("public async Task<IActionResult> Orders(", queueStart, StringComparison.Ordinal);

        Assert.True(queueEnd > queueStart);
        var queue = admin[queueStart..queueEnd];
        Assert.Contains("OrderStatus.PaymentReview", queue, StringComparison.Ordinal);
        Assert.Contains("p.PaymentStatus == PaymentStatus.RefundRequired", queue, StringComparison.Ordinal);
        Assert.DoesNotContain("!o.IsInternal", queue, StringComparison.Ordinal);
        Assert.Contains("ReconciledByUserId", payments, StringComparison.Ordinal);
        Assert.Contains("ExternalRefundReference", payments, StringComparison.Ordinal);
        Assert.Contains("ExternalRefundedAt", payments, StringComparison.Ordinal);
    }

    [Fact]
    public void Direct_wallet_recharge_is_not_rejected_or_clamped_by_the_balance_cap()
    {
        var wallet = File.ReadAllText(Path.Combine(RepositoryLayout.WebProjectDir, "Controllers", "WalletController.cs"));
        var rechargeStart = wallet.IndexOf("public async Task<IActionResult> Recharge(decimal amount)", StringComparison.Ordinal);
        var rechargeEnd = wallet.IndexOf("private async Task<WalletSettings>", rechargeStart, StringComparison.Ordinal);
        var payment = File.ReadAllText(Path.Combine(RepositoryLayout.WebProjectDir, "Controllers", "PaymentController.cs"));
        var admin = File.ReadAllText(Path.Combine(RepositoryLayout.WebProjectDir, "Controllers", "AdminController.cs"));

        Assert.True(rechargeStart >= 0 && rechargeEnd > rechargeStart);
        Assert.DoesNotContain("MaxWalletBalance", wallet[rechargeStart..rechargeEnd], StringComparison.Ordinal);
        Assert.Contains("wallet.Balance += order.TotalAmountSnapshot;", payment, StringComparison.Ordinal);

        var applyStart = admin.IndexOf("public async Task<IActionResult> ApplyCapturedPayment(", StringComparison.Ordinal);
        Assert.True(applyStart >= 0);
        var refundStart = admin.IndexOf("public async Task<IActionResult> RecordExternalRefund(", applyStart, StringComparison.Ordinal);
        Assert.True(refundStart > applyStart);
        Assert.Contains("wallet.Balance += order.TotalAmountSnapshot;", admin[applyStart..refundStart], StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWalletBalance", admin[applyStart..refundStart], StringComparison.Ordinal);
    }
}
