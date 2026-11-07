using System.Collections.Concurrent;
using System.Data;
using FourChang.Domain.Entities.Audit;
using FourChang.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

// Only registered in this standalone harness, never API Program/production DI.
sealed class SaveGate(int target)
{
 public readonly TaskCompletionSource Ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public readonly TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
 int hits;
 public async Task ArriveAsync(CancellationToken ct){if(Interlocked.Increment(ref hits)>=target)Ready.TrySetResult();await Release.Task.WaitAsync(TimeSpan.FromSeconds(25),ct);}
}
sealed class SaveControl : SaveChangesInterceptor
{
 public static SaveGate? Current;
 public static bool AuditFault;
 public static readonly ConcurrentBag<object> Observations=[];
 public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
 {
  var db=data.Context!;
  bool first=db.ChangeTracker.Entries<DonHang>().Any(e=>e.State==EntityState.Added);
  bool audit=db.ChangeTracker.Entries<NhatKyHeThong>().Any(e=>e.State==EntityState.Added&&e.Entity.HanhDong=="CREATE_CUSTOMER_ORDER");
  if(first||audit) {
   var tx=db.Database.CurrentTransaction??throw new SafetyStop("Create write outside transaction");
   ConsentRun.Check(tx.GetDbTransaction().IsolationLevel==IsolationLevel.Serializable,"EF Create transaction SERIALIZABLE");
   var connection=db.Database.GetDbConnection();await FixtureSqlGuard.VerifyAsync(connection,tx.GetDbTransaction(),ct:ct);
   await using var cmd=connection.CreateCommand();cmd.Transaction=tx.GetDbTransaction();
   cmd.CommandText="SELECT @@SPID,transaction_isolation_level,(SELECT transaction_id FROM sys.dm_tran_current_transaction) FROM sys.dm_exec_sessions WHERE session_id=@@SPID";
   await using var reader=await cmd.ExecuteReaderAsync(ct);await reader.ReadAsync(ct);
   if(Convert.ToInt32(reader.GetValue(1))!=4)throw new SafetyStop("Effective SQL isolation is not SERIALIZABLE");
   Observations.Add(new{context=db.ContextId.ToString(),efTransaction=tx.TransactionId,sqlSpid=Convert.ToInt32(reader.GetValue(0)),isolation=4,sqlTransaction=reader.GetInt64(2),phase=first?"before_order":"before_audit"});
  }
  if(audit&&AuditFault)throw new InjectedAuditFailure();
  if(first&&Current is {} gate)await gate.ArriveAsync(ct);
  return result;
 }
}
