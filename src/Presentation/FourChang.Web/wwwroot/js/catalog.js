/* ADM-03: chỉ đọc catalog chung và trình bày tùy chọn. Không ghi đơn, giữ kho hoặc xác nhận thanh toán. */
(() => {
  const root = document.querySelector('#catalog-live'); if (!root) return;
  const $ = s => document.querySelector(s);
  const normalize = s => s.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/đ/g,'d');
  const money = n => new Intl.NumberFormat('vi-VN').format(n) + 'đ';
  let snapshot = JSON.parse($('#catalog-snapshot').textContent), selected, variant, choice, category = 'Tất cả', refreshing = false;
  const toppings = new Map();
  // Chỉ dùng textContent cho dữ liệu quản trị, không đưa tên/mô tả vào HTML thực thi.
  function node(tag, text, cls) { const n = document.createElement(tag); if (text != null) n.textContent = text; if (cls) n.className = cls; return n; }
  function image(el, path, label) {
    el.className = 'drink-photo' + (el.classList.contains('selected-photo') ? ' selected-photo' : ''); el.style.backgroundImage = '';
    el.setAttribute('role','img'); el.setAttribute('aria-label', label);
    if (/^sprite:[0-5]$/.test(path || '')) el.classList.add('sprite-' + path.slice(7));
    else if (path && (/^https:\/\//.test(path) || path.startsWith('/assets/'))) {
      const img = node('img'); img.src = path; img.alt = label; img.style.cssText='width:100%;height:100%;object-fit:cover'; img.onerror=()=>img.remove();el.replaceChildren(img);
    }
  }
  function buttons(selector, values, current, change) {
    $(selector).replaceChildren(...values.map(([value,label,disabled]) => {
      const b=node('button',label,value===current?'selected':'');b.type='button';b.disabled=!!disabled;b.setAttribute('aria-pressed',String(value===current));b.onclick=()=>change(value);return b;
    }));
  }
  // Tìm/lọc dùng giá trong response API, không tạo bảng giá thứ hai ở trình duyệt.
  function renderGrid() {
    const term=normalize($('#menu-search').value.trim());const price=$('#price-filter').value;const stock=$('#stock-filter').value;const sort=$('#menu-sort').value;
    let items=snapshot.mon.filter(m=>m.cauHinh.length).filter(m=>{
      const p=m.cauHinh.find(v=>v.laMacDinh)||m.cauHinh[0], available=m.cauHinh.some(v=>v.conHang);
      return (!term||normalize(m.tenMon).includes(term))&&(category==='Tất cả'||m.tenDanhMuc===category)&&(price==='all'||(price==='under'?p.giaBan<45000:p.giaBan>=45000))&&(stock==='all'||(stock==='available'?available:!available));
    });
    if(sort!=='default')items.sort((a,b)=>((a.cauHinh.find(v=>v.laMacDinh)||a.cauHinh[0]).giaBan-(b.cauHinh.find(v=>v.laMacDinh)||b.cauHinh[0]).giaBan)*(sort==='asc'?1:-1));
    $('#product-grid').replaceChildren(...items.map(m=>{
      const v=m.cauHinh.find(v=>v.laMacDinh)||m.cauHinh[0], ok=m.cauHinh.some(v=>v.conHang);
      const card=node('article',null,'product-card'+(ok?'':' sold-out'));card.dataset.name=m.tenMon;card.dataset.category=m.tenDanhMuc;card.dataset.price=v.giaBan;card.dataset.available=String(ok);
      const photo=node('div');image(photo,m.duongDanAnh,m.tenMon);const info=node('div',null,'product-info'), caption=node('div');caption.append(node('h3',m.tenMon),node('strong',money(v.giaBan)));if(!ok)card.append(node('span','Tạm hết hàng','product-badge gray'));
      const add=node('button','+','round-add');add.dataset.menuId=m.maMon;add.setAttribute('aria-label','Chọn '+m.tenMon);add.disabled=!ok;add.onclick=()=>select(m);info.append(caption,add);card.append(photo,info);return card;
    }));
    $('#menu-empty').hidden=items.length>0;$('#menu-result-count').textContent=items.length+' món được chọn lọc';
    const page=root.querySelector('.pagination span');if(page)page.textContent='Trang 1 / 1 · '+items.length+' món';
  }
  function categories() {
    const values=['Tất cả',...new Set(snapshot.danhMuc?.map(c=>c.tenDanhMuc)||snapshot.mon.map(m=>m.tenDanhMuc))];if(!values.includes(category))category='Tất cả';
    buttons('#categories',values.map(v=>[v,v]),category,v=>{category=v;categories();renderGrid();});
  }
  function select(m) { selected=m;variant=m.cauHinh.find(v=>v.laMacDinh)||m.cauHinh[0];choice=variant?.tuyChon.find(c=>c.laMacDinh)||variant?.tuyChon[0];toppings.clear();renderOptions(); }
  function total() {
    const extra=[...toppings].reduce((sum,[id,qty])=>sum+(variant?.monThem.find(t=>t.maMonThem===id)?.giaMoiPhan||0)*qty,0);
    const amount=variant?(variant.giaBan+extra)*Number($('#quantity').textContent):0;
    $('#selected-price').textContent=variant?money(variant.giaBan):'—';$('#catalog-availability').textContent=choice?(choice.conHang?'Còn hàng':'Tạm hết hàng'):'';$('#catalog-subtotal').textContent=$('#catalog-total').textContent=variant?money(amount):'—';
  }
  function renderOptions() {
    $('#selected-name').textContent=selected?.tenMon||'Chọn món từ thực đơn';$('#selected-description').textContent=selected?.moTa||'';
    const photo=$('.selected-photo');photo.replaceChildren();image(photo,selected?.duongDanAnh,selected?.tenMon||'Món đang chọn');
    if(!variant){for(const s of ['#catalog-sizes','#catalog-temperatures','#catalog-sugar','#catalog-ice','#catalog-toppings'])$(s).replaceChildren();total();return;}
    buttons('#catalog-sizes',[...new Set(selected.cauHinh.map(v=>v.kichCo))].sort((a,b)=>['S','M','L'].indexOf(a)-['S','M','L'].indexOf(b)).map(s=>[s,s]),variant.kichCo,s=>{
      variant=selected.cauHinh.find(v=>v.kichCo===s&&v.nhietDo===variant.nhietDo)||selected.cauHinh.find(v=>v.kichCo===s);choice=variant.tuyChon.find(c=>c.laMacDinh)||variant.tuyChon[0];toppings.clear();renderOptions();
    });
    buttons('#catalog-temperatures',selected.cauHinh.filter(v=>v.kichCo===variant.kichCo).sort((a,b)=>(a.nhietDo==='HOT'?0:1)-(b.nhietDo==='HOT'?0:1)).map(v=>[v.nhietDo,v.nhietDo==='HOT'?'Nóng':'Lạnh']),variant.nhietDo,t=>{variant=selected.cauHinh.find(v=>v.kichCo===variant.kichCo&&v.nhietDo===t);choice=variant.tuyChon.find(c=>c.laMacDinh)||variant.tuyChon[0];toppings.clear();renderOptions();});
    buttons('#catalog-sugar',[...new Set(variant.tuyChon.map(c=>c.phanTramDuong))].map(n=>[n,n+'%']),choice?.phanTramDuong,s=>{choice=variant.tuyChon.find(c=>c.phanTramDuong===s&&c.phanTramDa===choice?.phanTramDa)||variant.tuyChon.find(c=>c.phanTramDuong===s);renderOptions();});
    buttons('#catalog-ice',variant.tuyChon.filter(c=>c.phanTramDuong===choice?.phanTramDuong).map(c=>[c.phanTramDa,c.phanTramDa+'%']),choice?.phanTramDa,i=>{choice=variant.tuyChon.find(c=>c.phanTramDuong===choice?.phanTramDuong&&c.phanTramDa===i);renderOptions();});
    $('#catalog-toppings').replaceChildren(...variant.monThem.map(t=>{
      const label=node('label',null,'catalog-topping'), check=node('input');check.type='checkbox';check.checked=toppings.has(t.maMonThem);check.disabled=!t.conHang;
      const qty=node('input');qty.type='number';qty.min=1;qty.max=t.gioiHanMoiLy;qty.value=toppings.get(t.maMonThem)||1;qty.disabled=!check.checked;qty.setAttribute('aria-label','Số phần '+t.tenMonThem);
      check.onchange=()=>{if(check.checked)toppings.set(t.maMonThem,Number(qty.value));else toppings.delete(t.maMonThem);qty.disabled=!check.checked;total();};
      qty.oninput=()=>{const n=Number(qty.value);if(Number.isInteger(n)&&n>=1&&n<=t.gioiHanMoiLy){qty.setCustomValidity('');toppings.set(t.maMonThem,n);total();}else qty.setCustomValidity('Số phần vượt giới hạn');};
      label.append(check,node('span',t.tenMonThem+' +'+money(t.giaMoiPhan)+(t.conHang?'':' · Tạm hết')),qty);return label;
    }));total();
  }
  const originalBanner=$('#catalog-banner').cloneNode(true);
  function banner() {
    const b=snapshot.banner[0], hero=$('#catalog-banner');
    if(!b){hero.replaceChildren(...[...originalBanner.childNodes].map(n=>n.cloneNode(true)));return;}
    const text=node('div');text.append(node('span','CÀ PHÊ NGON MỖI NGÀY','eyebrow'),node('h1',b.tenBanner),node('p',b.noiDung||''));
    if(b.lienKet){const a=node('a','Khám phá →','button cream');a.href=b.lienKet;text.append(a);}const photo=node('div');image(photo,b.duongDanAnh,b.tenBanner);photo.classList.add('hero-cup');hero.replaceChildren(text,photo);
  }
  async function refresh() {
    if(refreshing)return;refreshing=true;
    try {
      const response=await fetch('/catalog/data',{cache:'no-store'});if(!response.ok)throw Error();const next=await response.json();
      const old=JSON.stringify(selected), currentId=selected?.maMon, currentVariant=variant?.maCauHinhMon, sugar=choice?.phanTramDuong, ice=choice?.phanTramDa;
      snapshot=next;selected=next.mon.find(m=>m.maMon===currentId);variant=selected?.cauHinh.find(v=>v.maCauHinhMon===currentVariant)||selected?.cauHinh.find(v=>v.laMacDinh);choice=variant?.tuyChon.find(c=>c.phanTramDuong===sugar&&c.phanTramDa===ice)||variant?.tuyChon.find(c=>c.laMacDinh);
      for(const [id,n]of toppings){const t=variant?.monThem.find(t=>t.maMonThem===id);if(!t||!t.conHang||n>t.gioiHanMoiLy)toppings.delete(id);}
      if(currentId&&old!==JSON.stringify(selected))$('#catalog-message').textContent='Thực đơn đã cập nhật. Vui lòng kiểm tra lại giá và tùy chọn.';else $('#catalog-message').textContent='';
      categories();renderGrid();renderOptions();banner();
    } catch {$('#catalog-message').textContent='Chưa tải được thực đơn mới. Dữ liệu đang xem có thể đã cũ.';}
    finally {refreshing=false;}
  }
  ['#menu-search','#price-filter','#stock-filter','#menu-sort'].forEach(s=>$(s).addEventListener(s==='#menu-search'?'input':'change',renderGrid));
  $('#reset-filter').onclick=()=>{$('#menu-search').value='';$('#price-filter').value=$('#stock-filter').value='all';category='Tất cả';categories();renderGrid();};
  document.querySelectorAll('[data-qty]').forEach(b=>b.addEventListener('click',total));
  categories();renderGrid();banner();const initial=snapshot.mon.find(m=>m.cauHinh.some(v=>v.conHang))||snapshot.mon.find(m=>m.cauHinh.length);if(initial)select(initial);
  const events=new EventSource('/catalog/events');events.onmessage=refresh;events.onerror=()=>{$('#catalog-message').textContent='Đang kết nối lại thực đơn…';};
  window.addEventListener('online',refresh);window.addEventListener('focus',refresh);window.addEventListener('pagehide',()=>events.close(),{once:true});
})();
