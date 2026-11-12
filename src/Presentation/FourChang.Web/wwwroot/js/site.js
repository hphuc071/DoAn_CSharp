/* Presentation events and a read-only legacy-cart notice. No storage mutation or business writes. */
(() => {
  const $ = (s, root = document) => root.querySelector(s);
  const $$ = (s, root = document) => [...root.querySelectorAll(s)];
  let toastTimer;
  function toast(message) {const t = $('#toast');t.textContent = message;t.hidden = false;clearTimeout(toastTimer);toastTimer = setTimeout(() => t.hidden = true, 4500);}
  $$('[data-choice]').forEach(group => group.addEventListener('click', e => {const button = e.target.closest('button');if(!button)return;$$('button',group).forEach(b=>b.classList.toggle('selected',b===button));}));
  $$('[data-tabs]').forEach(tabs => tabs.addEventListener('click', e => {const b=e.target.closest('[data-tab]');if(!b)return;$$('[data-tab]',tabs).forEach(x=>x.classList.toggle('selected',x===b));tabs.parentElement.querySelectorAll(':scope > .tab-panel').forEach(p=>p.hidden=p.id!==b.dataset.tab);}));
  $$('[data-qty]').forEach(b=>b.addEventListener('click',()=>{const q=$('#quantity');q.textContent=Math.max(1,Math.min(99,Number(q.textContent)+Number(b.dataset.qty)));}));
  $$('[data-local-minus],[data-local-plus]').forEach(b=>b.addEventListener('click',()=>toast('Điều chỉnh số lượng là bản xem trước. Chưa lưu giỏ hoặc tính giá nghiệp vụ.')));
  $$('[data-demo-form]').forEach(form=>form.addEventListener('submit',e=>{e.preventDefault();toast('Đây là bản mẫu giao diện. Thông tin chưa được lưu hoặc gửi đi.');}));
  $$('[data-preview]').forEach(b=>b.addEventListener('click',()=>{const d=$('#preview-dialog');$('#dialog-title').textContent=b.dataset.preview;$('#dialog-copy').textContent=b.dataset.copy||'Xem trước thao tác. Chưa thay đổi dữ liệu, xuất tệp, in hoặc gọi dịch vụ thật.';const container=$('#dialog-fields');container.replaceChildren();(b.dataset.fields||'').split('|').filter(Boolean).forEach(name=>{const label=document.createElement('label');label.className='field';label.append(document.createTextNode(name));const input=document.createElement('input');input.placeholder=name;input.type=name.toLowerCase().includes('mật khẩu')?'password':'text';label.append(input);container.append(label);});d.showModal();}));
  $$('[data-payment-state]').forEach(b=>b.addEventListener('click',()=>{$('#payment-status').textContent=b.dataset.paymentState;$('#payment-status').className='payment-status '+(b.dataset.paymentState.startsWith('Đã xác minh đủ')?'success-note':'notice');}));
  $('#scenario')?.addEventListener('change',e=>{const key=e.target.value;const msg=$('#scenario-message');const messages={empty:'Không có dữ liệu phù hợp. Hãy thử thay bộ lọc hoặc quay lại thực đơn.',offline:'Mất kết nối · Nội dung đang hiển thị là dữ liệu mẫu có thể cũ. Các thao tác ghi chưa khả dụng.',error:'Không thể xử lý yêu cầu · Thông tin nháp được giữ để kiểm tra hoặc thử lại.',loading:'Đang tải dữ liệu…'};msg.textContent=messages[key]||'';msg.hidden=key==='normal';document.querySelectorAll('main > :not(#scenario-message)').forEach(el=>el.classList.toggle('ui-loading',key==='loading'));if($('#product-grid')){$('#product-grid').hidden=key==='empty';$('#menu-empty').hidden=key!=='empty';}});
  try{if(['/','/cart','/checkout'].includes(location.pathname.toLowerCase())&&localStorage.getItem('fourchang_cart_v1')!==null){const notice=document.createElement('p');notice.className='notice';notice.setAttribute('role','status');notice.textContent='Máy này còn giỏ theo định dạng cũ. Dữ liệu được giữ nguyên; giỏ hiện tại không tự nhập hay xóa giỏ cũ. Vui lòng đối chiếu món và cấu hình trước khi đặt, hoặc nhờ hỗ trợ nếu có thao tác chưa rõ kết quả.';document.querySelector('main')?.prepend(notice);}}catch{}
  const search=$('#menu-search');if(search){const sortFromUrl=new URLSearchParams(location.search).get('sort');if(['newest','bestseller'].includes(sortFromUrl))$('#menu-sort').value=sortFromUrl;let category='Tất cả';let cards=$$('.product-card');window.addEventListener('fourchang:catalog-refresh',()=>{cards=$$('.product-card');category=$('#categories').querySelector('.selected')?.dataset.category??'Tất cả';filter();});function filter(){const normalize=s=>s.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/đ/g,'d');const keyword=normalize(search.value);const price=$('#price-filter').value;let count=0;cards.forEach(c=>{const ok=normalize(c.dataset.name).includes(keyword)&&(category==='Tất cả'||c.dataset.category===category)&&($('#stock-filter').value==='all'||c.dataset.available===($('#stock-filter').value==='available'?'true':'false'))&&(price==='all'||(price==='under'?Number(c.dataset.price)<45000:Number(c.dataset.price)>=45000));c.hidden=!ok;if(ok)count++;});$('#menu-empty').hidden=count>0;}search.addEventListener('input',filter);$('#stock-filter').addEventListener('change',filter);$('#price-filter').addEventListener('change',filter);$('#categories').addEventListener('click',e=>{const b=e.target.closest('button');if(!b)return;category=b.dataset.category;$$('button',$('#categories')).forEach(x=>x.classList.toggle('selected',x===b));filter();});$('#reset-filter').addEventListener('click',()=>{search.value='';category='Tất cả';$('#price-filter').value='all';$('#stock-filter').value='all';if($('#scenario'))$('#scenario').value='normal';$('#scenario-message').hidden=true;$$('button',$('#categories')).forEach((x,i)=>x.classList.toggle('selected',i===0));$('#product-grid').hidden=false;filter();});$('#menu-sort').addEventListener('change',e=>{const sorted=[...cards];if(e.target.value==='newest'||e.target.value==='bestseller'){const url=new URL(location.href);url.searchParams.set('sort',e.target.value);location.assign(url.toString());return;}if(e.target.value==='asc'||e.target.value==='desc')sorted.sort((a,b)=>(Number(a.dataset.price)-Number(b.dataset.price))*(e.target.value==='asc'?1:-1));sorted.forEach(c=>$('#product-grid').append(c));});}
})();

// Đồng bộ con số trên header ở mọi trang từ cùng giỏ đang dùng; chỉ đọc, không sửa dữ liệu giỏ.
(() => {
  const key = "fourchang.cart-draft.v1";
  /** Hiển thị tổng số ly hợp lệ; giỏ lỗi hiện dấu — để không báo nhầm là giỏ trống. */
  function updateCartBadge() {
    const badge = document.getElementById("cart-badge");
    if (!badge) return;
    try {
      const rows = JSON.parse(localStorage.getItem(key) || "[]");
      if (!Array.isArray(rows)) throw new Error("Invalid cart");
      const count = rows.reduce((sum, item) => sum + (Number.isSafeInteger(item?.quantity) && item.quantity >= 1 && item.quantity <= 99 ? item.quantity : 0), 0);
      badge.textContent = Number.isSafeInteger(count) ? String(count) : "—";
    } catch { badge.textContent = "—"; }
  }
  window.addEventListener("cart:changed", updateCartBadge);
  window.addEventListener("storage", event => { if (event.key === key || event.key === null) updateCartBadge(); });
  window.addEventListener("pageshow", updateCartBadge);
  updateCartBadge();
})();
