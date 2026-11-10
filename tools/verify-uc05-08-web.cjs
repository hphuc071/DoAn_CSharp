/** Run from solution root: node --test tools/verify-uc05-08-web.cjs
 * UI-only smoke tests. They do not substitute for .NET, database or bank tests.
 */
'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const root = path.resolve(__dirname, '..', 'src', 'Presentation', 'FourChang.Web');
const script = (relative) => fs.readFileSync(path.join(root, relative), 'utf8');

function element(tag='div') {
  return {
    tagName:tag, hidden:false, disabled:false, textContent:'', value:'', checked:false,
    dataset:{}, children:[], listeners:{},
    append(...children){this.children.push(...children);},
    replaceChildren(...children){this.children=[...children];},
    addEventListener(type, callback){this.listeners[type]=callback;},
    setAttribute(){},
  };
}

test('UC05: xóa hết giỏ báo cart:changed nhưng không xóa thao tác Checkout đang chờ', async () => {
  let cartRaw = JSON.stringify([{productId:2,variantId:10,name:'Món',size:'M',temperature:'Lạnh',sugar:'50%',ice:'50%',quantity:2,listedPrice:20000,toppings:[],draftVersion:2}]);
  const checkoutIntent = '{"operation":"pending"}';
  const storage = new Map([['fourchang.cart-draft.v1', cartRaw],['fourchang.checkout-intent.v1',checkoutIntent]]);
  const localStorage = {getItem:k=>storage.get(k)??null, setItem:(k,v)=>storage.set(k,v), removeItem:k=>storage.delete(k)};
  const nodes = new Map();
  const get = (id) => {if(!nodes.has(id))nodes.set(id,element());return nodes.get(id);};
  let changed = 0;
  const handlers = {};
  const window = {FourChangCart:{key:'fourchang.cart-draft.v1',raw:()=>storage.get('fourchang.cart-draft.v1')||'[]',parse:JSON.parse,
    total:x=>x.listedPrice*x.quantity,add:(a,b)=>a+b,integer:(n,min,max)=>Number.isSafeInteger(n)&&n>=min&&n<=max,
    format:n=>String(n)+'đ'},addEventListener:(k,cb)=>handlers[k]=cb,
    dispatchEvent:(e)=>{if(e.type==='cart:changed')changed++;handlers[e.type]?.(e);}};
  const document = {getElementById:get,createElement:element};
  const context={window,document,localStorage,navigator:{locks:{request:async (_key,cb)=>cb()}},confirm:()=>true,
    Event:class{constructor(type){this.type=type;}},console};
  vm.runInNewContext(script('wwwroot/js/cart-page.js'),context,{filename:'cart-page.js'});
  await get('clear-cart').onclick();
  assert.equal(storage.get('fourchang.cart-draft.v1'),'[]');
  assert.equal(storage.get('fourchang.checkout-intent.v1'),checkoutIntent);
  assert.equal(changed,1);
  assert.equal(get('cart-empty').hidden,false);
});

test('UC06→UC07: biên nhận chờ tiền dẫn đến trang thanh toán của đúng mã đơn', () => {
  const receipt={maDonHang:2468,maDonHienThi:'FOUR-2468',trangThai:'PENDING_PAYMENT',tienHang:45000,phiGiaoHang:0,tongCong:45000,hetHanLuc:null};
  const intent={version:1,ownerId:'42',cartRaw:'[]',command:{operation:'12345678-1234-1234-1234-123456789012',acceptedTotal:45000,
    ownerProof:'proof',order:{hinhThucNhanMon:'TAKEAWAY',tenNguoiNhan:'Người nhận',dienThoaiNguoiNhan:'0912345678'}},receipt};
  const storage=new Map([['fourchang.checkout-intent.v1',JSON.stringify(intent)],['fourchang.cart-draft.v1','[]']]);
  const localStorage={getItem:k=>storage.get(k)??null,removeItem:k=>storage.delete(k),setItem:(k,v)=>storage.set(k,v)};
  const nodes=new Map();
  const get=(id)=>{if(!nodes.has(id))nodes.set(id,element());return nodes.get(id);};
  const button=get('checkout-proceed');button.dataset={orderCreationEnabled:'true',customerId:'42',ownerProof:'proof'};
  const document={getElementById:get,querySelector:s=>s.includes('antiforgery')?{value:'csrf'}:{dataset:{tab:'takeaway'}}};
  const window={addEventListener(){},dispatchEvent(){},fourchangCheckoutQuote:null};
  const context={window,document,localStorage,navigator:{locks:{request(){}}},Event:class{},console,
    crypto:{randomUUID(){throw Error('should not create an order');}},fetch(){throw Error('should not POST during rendering');}};
  vm.runInNewContext(script('wwwroot/js/checkout-order.js'),context,{filename:'checkout-order.js'});
  assert.equal(get('checkout-receipt').hidden,false);
  assert.equal(get('checkout-receipt-payment').href,'/Checkout/Payment?orderId=2468');
  assert.equal(get('checkout-proceed').disabled,true);
});

test('UC07: nút Tôi đã chuyển chỉ tải trang GET, không tự đánh dấu PAID', () => {
  const page=script('Pages/Checkout/Payment.cshtml');
  assert.match(page,/Tôi đã chuyển — kiểm tra trạng thái từ Backend/);
  assert.match(page,/Request\.Path \+ Request\.QueryString/);
  assert.doesNotMatch(page,/asp-page-handler="(MarkPaid|ConfirmPaid|Paid)"/i);
});

test('UC06: đổi sang giao hàng không hiển thị giá mang đi đã báo trước', async () => {
  const nodes=new Map();
  const get=id=>{ if(!nodes.has(id)) nodes.set(id,element()); return nodes.get(id); };
  get('checkout-base-subtotal').textContent='54.000đ';
  get('shipping-fee').textContent='0đ';
  get('checkout-total').textContent='54.000đ';
  get('checkout-base-subtotal').parentElement={querySelector:()=>element()};
  let selectedMode='delivery';
  const handlers={};
  const tabs={querySelector:()=>({dataset:{tab:selectedMode}}),addEventListener:(k,cb)=>handlers[k]=cb};
  const document={getElementById:get,querySelector:s=>s==='[data-tabs]'?tabs:s.includes('antiforgery')?{value:'csrf'}:null};
  const window={fourchangCheckoutQuote:{old:true}, addEventListener:(k,cb)=>handlers[k]=cb,
    dispatchEvent(){} };
  const context={window,document,localStorage:{getItem:()=>null},AbortController,Event:class{constructor(type){this.type=type;}},console};
  vm.runInNewContext(script('wwwroot/js/checkout-quote.js'),context);
  handlers.pageshow();
  await Promise.resolve();
  assert.equal(get('checkout-base-subtotal').textContent,'Chưa xác nhận');
  assert.equal(get('shipping-fee').textContent,'Chưa báo giá');
  assert.equal(get('checkout-total').textContent,'Chưa thể đặt đơn');
  assert.equal(window.fourchangCheckoutQuote,null);
  assert.match(get('checkout-price-warning').textContent,/phí giao/);
});

test('UC06: ghi chú >500 ký tự bị chặn trước POST, không bị cắt im lặng', async () => {
  const item={draftVersion:2,productId:1,variantId:2,quantity:1,listedPrice:45000,sugar:'50%',ice:'50%',
    name:'Món mẫu',note:'a'.repeat(501),toppings:[]};
  const key='fourchang.cart-draft.v1';
  const cartRaw=JSON.stringify([item]);
  const nodes=new Map();
  const get=id=>{if(!nodes.has(id)) nodes.set(id,element());return nodes.get(id);};
  get('checkout-base-subtotal').parentElement={querySelector:()=>element()};
  const tabs={querySelector:()=>({dataset:{tab:'takeaway'}}),addEventListener(){}};
  const handlers={};
  let postCount=0;
  const fetch=async (url,options) => {
    if(options?.method==='POST'){postCount++;throw Error('Must not POST invalid note');}
    assert.match(url,/Variants/);
    return {ok:true,json:async()=>[{
      maCauHinhMon:2,available:true,tuyChonPhaChe:[{phanTramDuong:50,phanTramDa:50}],toppings:[]
    }]};
  };
  const document={getElementById:get,querySelector:s=>s==='[data-tabs]'?tabs:s.includes('antiforgery')?{value:'csrf'}:null};
  const window={fourchangCheckoutQuote:null,addEventListener:(k,cb)=>handlers[k]=cb,dispatchEvent(){}};
  const context={window,document,localStorage:{getItem:k=>k===key?cartRaw:null},fetch,AbortController,
    Event:class{constructor(type){this.type=type;}},console};
  vm.runInNewContext(script('wwwroot/js/checkout-quote.js'),context);
  handlers.pageshow();
  // Let Promise.all/fetch and the async catch settle.
  for(let i=0;i<12;i++) await Promise.resolve();
  assert.equal(postCount,0);
  assert.equal(window.fourchangCheckoutQuote,null);
  assert.match(get('checkout-price-warning').textContent,/500 ký tự/);
});

test('UC06: Backend trả phí giao >0 cho TAKEAWAY thì không chấp nhận báo giá', async () => {
  const item={draftVersion:2,productId:1,variantId:2,quantity:1,listedPrice:45000,sugar:'50%',ice:'50%',
    name:'Món mẫu',note:'',toppings:[]};
  const key='fourchang.cart-draft.v1',cartRaw=JSON.stringify([item]);
  const nodes=new Map();
  const get=id=>{if(!nodes.has(id))nodes.set(id,element());return nodes.get(id);};
  get('checkout-base-subtotal').parentElement={querySelector:()=>element()};
  const tabs={querySelector:()=>({dataset:{tab:'takeaway'}}),addEventListener(){}};
  const handlers={};
  const fetch=async (url,options)=>options?.method==='POST'
    ?{ok:true,json:async()=>({tienHang:45000,phiGiaoHang:5000,tongCong:50000})}
    :{ok:true,json:async()=>[{maCauHinhMon:2,available:true,tuyChonPhaChe:[{phanTramDuong:50,phanTramDa:50}],toppings:[]}]};
  const document={getElementById:get,querySelector:s=>s==='[data-tabs]'?tabs:s.includes('antiforgery')?{value:'csrf'}:null};
  const window={fourchangCheckoutQuote:null,addEventListener:(k,cb)=>handlers[k]=cb,dispatchEvent(){}};
  const context={window,document,localStorage:{getItem:k=>k===key?cartRaw:null},fetch,AbortController,
    Event:class{constructor(type){this.type=type;}},console};
  vm.runInNewContext(script('wwwroot/js/checkout-quote.js'),context);
  handlers.pageshow();
  for(let i=0;i<14;i++) await Promise.resolve();
  assert.equal(window.fourchangCheckoutQuote,null);
  assert.equal(get('checkout-total').textContent,'Chưa thể báo giá');
  assert.match(get('checkout-price-warning').textContent,/không được tính phí giao/);
});

test('UC05: giỏ hỏng không bị hiện như giỏ rỗng hay tổng tiền cũ', () => {
  const nodes=new Map();
  const get=id=>{if(!nodes.has(id))nodes.set(id,element());return nodes.get(id);};
  get('cart-base-total').textContent='45000đ';
  get('cart-badge').textContent='7';
  const C={key:'fourchang.cart-draft.v1',raw:()=>'{broken',parse:()=>{throw Error('invalid JSON')}};
  const window={FourChangCart:C,addEventListener(){}};
  const document={getElementById:get,createElement:element};
  vm.runInNewContext(script('wwwroot/js/cart-page.js'),{window,document,console});
  assert.equal(get('cart-empty').hidden,true);
  assert.equal(get('cart-base-total').textContent,'Không xác định');
  assert.equal(get('cart-badge').textContent,'!');
  assert.match(get('cart-status').textContent,/Chưa xóa hoặc ghi đè/);
});

test('UC07: QR có hạn ISO +00:00 hợp lệ không bị ẩn vì lỗi parsing', () => {
  const expiry=new Date(Date.now()+1000*120).toISOString().replace(/Z$/,'+00:00');
  const qr=element();qr.getAttribute=()=>expiry;
  const notice=element();notice.hidden=true;
  const handlers={};let timers=0;
  const document={querySelectorAll:()=>[qr],getElementById:id=>id==='order-snapshot-connection'?notice:element(),
    addEventListener:(type,callback)=>handlers[type]=callback,hidden:false};
  const window={addEventListener:(type,callback)=>handlers[type]=callback,
    setTimeout:()=>{timers++;},location:{reload(){}}};
  vm.runInNewContext(script('wwwroot/js/order-snapshot.js'),{window,document,navigator:{onLine:true},Date,console});
  assert.equal(qr.hidden,false);
  assert.ok(timers>0);
});
