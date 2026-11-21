// ---- Đổi giao diện sáng/tối (lựa chọn lưu trong localStorage; mặc định theo hệ điều hành) ----
(function () {
  var root = document.documentElement;
  var btn = document.getElementById('theme-toggle');
  if (!btn) return;

  function apply(theme) {
    root.setAttribute('data-theme', theme);
    btn.setAttribute('aria-pressed', theme === 'dark' ? 'true' : 'false');
  }
  apply(root.getAttribute('data-theme') || 'light');

  btn.addEventListener('click', function () {
    var next = root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
    apply(next);
    try { localStorage.setItem('theme', next); } catch (e) { /* bỏ qua nếu bị chặn */ }
  });

  // Nếu người dùng chưa tự chọn, đi theo thay đổi của hệ điều hành.
  var mq = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;
  if (mq && mq.addEventListener) {
    mq.addEventListener('change', function (e) {
      var stored = null;
      try { stored = localStorage.getItem('theme'); } catch (err) { }
      // Giao diện BLK mặc định tối (data-theme-default) nên không đi theo hệ điều hành.
      if (!stored && !root.hasAttribute('data-theme-default')) apply(e.matches ? 'dark' : 'light');
    });
  }
})();

// ---- Comment reply: gán ParentId và hiện banner "Replying to ..." ----
(function () {
  var form = document.getElementById('comment-form');
  if (!form) return;

  var parentInput = document.getElementById('ParentId');
  var banner = document.getElementById('reply-banner');
  var nameEl = document.getElementById('reply-to-name');
  var cancel = document.getElementById('cancel-reply');

  function setReply(id, author) {
    parentInput.value = id || '';
    if (id) { nameEl.textContent = author; banner.hidden = false; }
    else { banner.hidden = true; }
  }

  document.querySelectorAll('.reply-link').forEach(function (link) {
    link.addEventListener('click', function () {
      setReply(link.getAttribute('data-comment-id'), link.getAttribute('data-author'));
    });
  });
  if (cancel) cancel.addEventListener('click', function () { setReply('', ''); });

  if (parentInput.value) {
    var existing = document.querySelector('.reply-link[data-comment-id="' + parentInput.value + '"]');
    setReply(parentInput.value, existing ? existing.getAttribute('data-author') : '');
  }
})();

// ---- Giao diện Indigo: menu mobile + nút "lên đầu trang" ----
(function () {
  var burger = document.getElementById('ix-burger');
  var header = document.querySelector('.ix-header');
  if (burger && header) {
    burger.addEventListener('click', function () {
      var open = header.classList.toggle('open');
      burger.setAttribute('aria-expanded', open ? 'true' : 'false');
    });
  }
  var top = document.getElementById('ix-top');
  if (top) {
    top.addEventListener('click', function (e) {
      e.preventDefault();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }
})();

// ---- Giao diện BLK: menu mobile, header đặc lại khi cuộn, nút "lên đầu trang" ----
(function () {
  var header = document.querySelector('.bk-header');
  if (!header) return;
  var burger = document.getElementById('bk-burger');
  if (burger) {
    burger.addEventListener('click', function () {
      var open = header.classList.toggle('open');
      burger.setAttribute('aria-expanded', open ? 'true' : 'false');
    });
  }
  function onScroll() { header.classList.toggle('scrolled', window.scrollY > 8); }
  onScroll();
  window.addEventListener('scroll', onScroll, { passive: true });
  var top = document.getElementById('bk-top');
  if (top) {
    top.addEventListener('click', function (e) {
      e.preventDefault();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }
})();
