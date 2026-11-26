(function () {
  var el = document.getElementById('editor');
  if (!el || !window.tinymce) return;

  var csrf = document.querySelector('meta[name="csrf-token"]');
  var token = csrf ? csrf.content : '';
  var dark = document.documentElement.getAttribute('data-theme') === 'dark';
  var lang = el.getAttribute('data-lang');

  function msg(key, fallback) { return el.getAttribute('data-msg-' + key) || fallback; }

  // Nhận watch?v=, youtu.be/, /embed/, /shorts/, /live/ (kèm t=90 / t=1m30s) hoặc chính ID 11 ký tự.
  function parseYouTube(input) {
    input = (input || '').trim();
    var ID = /^[A-Za-z0-9_-]{11}$/;
    if (ID.test(input)) return { id: input, start: 0 };
    var u;
    try { u = new URL(/^https?:\/\//i.test(input) ? input : 'https://' + input); } catch (e) { return null; }
    var host = u.hostname.replace(/^(www|m|music)\./, '');
    var id = null, m;
    if (host === 'youtu.be') id = u.pathname.split('/')[1];
    else if (host === 'youtube.com' || host === 'youtube-nocookie.com') {
      if (u.pathname === '/watch') id = u.searchParams.get('v');
      else if ((m = u.pathname.match(/^\/(?:embed|shorts|live|v)\/([A-Za-z0-9_-]{11})/))) id = m[1];
    }
    if (!id || !ID.test(id)) return null;

    var t = u.searchParams.get('t') || u.searchParams.get('start') || '';
    var start = 0;
    if (/^\d+$/.test(t)) start = parseInt(t, 10);
    else if (t && (m = t.match(/^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?$/))) start = (+m[1] || 0) * 3600 + (+m[2] || 0) * 60 + (+m[3] || 0);
    return { id: id, start: start };
  }

  function youtubeHtml(v) {
    var src = 'https://www.youtube-nocookie.com/embed/' + v.id + (v.start ? '?start=' + v.start : '');
    return '<div class="video-embed"><iframe src="' + src + '" title="YouTube video" loading="lazy" ' +
      'allow="accelerometer; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" ' +
      'referrerpolicy="strict-origin-when-cross-origin" allowfullscreen="allowfullscreen"></iframe></div><p></p>';
  }

  // Upload bằng XHR để có tiến độ (video có thể nặng hàng trăm MB).
  function upload(url, file, onProgress, filename) {
    return new Promise(function (resolve, reject) {
      var data = new FormData();
      data.append('file', file, filename || file.name);
      data.append('__RequestVerificationToken', token);
      var xhr = new XMLHttpRequest();
      xhr.open('POST', url);
      xhr.withCredentials = true;
      if (onProgress) xhr.upload.onprogress = function (e) { if (e.lengthComputable) onProgress(Math.round(e.loaded / e.total * 100)); };
      xhr.onerror = function () { reject(msg('upload-failed', 'Upload failed.')); };
      xhr.onload = function () {
        var body = null;
        try { body = JSON.parse(xhr.responseText); } catch (e) { /* HTML (vd. 413) */ }
        if (xhr.status >= 200 && xhr.status < 300 && body && body.url) resolve(body);
        else reject(xhr.status === 413 ? msg('too-large', 'File too large for the server limit.')
          : (body && body.error) || msg('upload-failed', 'Upload failed.'));
      };
      xhr.send(data);
    });
  }

  function fmtSize(n) {
    return n >= 1048576 ? (n / 1048576).toFixed(1).replace(/\.0$/, '') + ' MB' : Math.max(1, Math.round(n / 1024)) + ' KB';
  }

  function notifyError(editor, text) {
    editor.notificationManager.open({ text: String(text), type: 'error', timeout: 6000 });
  }

  // Hộp thoại chọn file: tab "Tải lên" (kéo thả / chọn file) và tab "Thư viện" (file đã upload trước đó).
  // kind: 'file' (zip/Office/PDF...), 'image' hoặc 'video'. onPick nhận { url, name, size }.
  function openPicker(editor, kind, onPick) {
    var endpoint = kind === 'image' ? '/admin/media/uploadimage' : kind === 'video' ? '/admin/media/uploadvideo' : '/admin/media/uploadfile';
    fetch('/admin/media/browse?kind=' + encodeURIComponent(kind), { credentials: 'same-origin' })
      .then(function (r) { return r.ok ? r.json() : []; })
      .catch(function () { return []; })
      .then(function (items) {
        var byUrl = {};
        var options = [{ value: '', text: msg('pick-choose', '— choose a file —') }];
        items.forEach(function (it) {
          byUrl[it.url] = it;
          options.push({ value: it.url, text: it.name + ' (' + fmtSize(it.size) + ')' });
        });

        editor.windowManager.open({
          title: msg('pick-title', 'Insert file'),
          size: 'medium',
          body: {
            type: 'tabpanel',
            tabs: [
              { name: 'upload', title: msg('tab-upload', 'Upload'), items: [{ type: 'dropzone', name: 'files', label: msg('pick-drop', 'Drop a file here or click to choose') }] },
              { name: 'library', title: msg('tab-library', 'Library'), items: [{ type: 'selectbox', name: 'lib', label: msg('tab-library', 'Library'), items: options }] }
            ]
          },
          buttons: [
            { type: 'cancel', text: 'Cancel' },
            { type: 'submit', text: 'Insert', buttonType: 'primary' }
          ],
          onSubmit: function (api) {
            var d = api.getData();
            var files = d.files || [];
            if (files.length) {
              api.block(msg('uploading-file', 'Uploading file…'));
              upload(endpoint, files[0], null).then(function (r) {
                api.close();
                onPick({ url: r.url, name: r.name || files[0].name, size: r.size || files[0].size });
              }).catch(function (err) {
                api.unblock();
                notifyError(editor, err);
              });
              return;
            }
            if (d.lib && byUrl[d.lib]) {
              api.close();
              onPick(byUrl[d.lib]);
              return;
            }
            notifyError(editor, msg('pick-none', 'Upload a file or choose one from the library.'));
          }
        });
      });
  }

  // Chèn link tải file: dùng chữ đang bôi đen làm nhãn (nếu có), không thì dùng tên file.
  function insertFileLink(editor, it) {
    var enc = editor.dom.encode;
    var label = editor.selection.getContent({ format: 'text' }).trim() || it.name;
    var size = it.size ? ' <small>(' + fmtSize(it.size) + ')</small>' : '';
    editor.insertContent('<a href="' + enc(it.url) + '" download="' + enc(it.name) + '">' + enc(label) + '</a>' + size + '&nbsp;');
  }

  var cfg = {
    target: el,
    base_url: '/lib/tinymce',
    suffix: '.min',
    skin: dark ? 'oxide-dark' : 'oxide',
    content_css: dark ? 'dark' : 'default',
    height: 560,
    min_height: 360,
    resize: true,
    branding: false,
    promotion: false,
    menubar: 'edit view insert format table',
    toolbar_mode: 'wrap',
    plugins: 'advlist autolink lists link image charmap searchreplace visualblocks code fullscreen preview table wordcount codesample emoticons',
    toolbar: 'undo redo | blocks fontfamily fontsize | bold italic underline strikethrough | forecolor backcolor | ' +
             'alignleft aligncenter alignright alignjustify | bullist numlist outdent indent | ' +
             'link image attachfile ytb videoup table | blockquote codesample hrline emoticons charmap | removeformat | ' +
             'visualblocks searchreplace preview fullscreen code',

    block_formats: 'Paragraph=p; Heading 2=h2; Heading 3=h3; Heading 4=h4; Preformatted=pre',
    // Chỉ dùng font hệ thống an toàn: trang công khai không tải web font, nên font phải có sẵn trên máy người đọc.
    font_family_formats: 'Arial=arial,helvetica,sans-serif; Verdana=verdana,geneva,sans-serif; Tahoma=tahoma,arial,sans-serif; ' +
      'Trebuchet MS=trebuchet ms,sans-serif; Times New Roman=times new roman,times,serif; Georgia=georgia,serif; ' +
      'Palatino=palatino linotype,palatino,serif; Courier New=courier new,courier,monospace; Comic Sans MS=comic sans ms,cursive',
    font_size_formats: '10px 12px 14px 16px 18px 20px 24px 28px 32px 40px 48px',
    custom_colors: true,
    color_cols: 8,

    // Giữ nguyên URL /uploads/... (không chuyển thành đường dẫn tương đối).
    relative_urls: false,
    convert_urls: false,
    image_advtab: true,
    image_caption: true,
    link_default_target: '_blank',
    link_assume_external_targets: 'https',
    table_default_styles: { width: '100%' },
    // YouTube iframe + thẻ <video> do nút YouTube/Video chèn vào.
    extended_valid_elements:
      'a[href|target|rel|download|title|class|style],' +
      'iframe[src|title|loading|allow|referrerpolicy|allowfullscreen|frameborder|width|height|class|style],' +
      'video[src|controls|preload|playsinline|poster|width|height|class|style|muted|loop|autoplay],source[src|type]',

    content_style:
      'body{font-family:system-ui,-apple-system,"Segoe UI",Roboto,Arial,sans-serif;font-size:1.05rem;line-height:1.7;margin:1rem}' +
      'img,video{max-width:100%;height:auto}video{display:block;background:#000;border-radius:10px}' +
      'table{border-collapse:collapse}th,td{border:1px solid #8884;padding:.4rem .6rem}' +
      'blockquote{margin:1.2rem 0;padding:.2rem 1rem;border-left:3px solid #8886}' +
      'pre{padding:.8rem 1rem;border-radius:8px;overflow:auto}' +
      '.video-embed{position:relative;width:100%;aspect-ratio:16/9;background:#000;border-radius:10px;overflow:hidden}' +
      '.video-embed iframe{position:absolute;inset:0;width:100%;height:100%;border:0}',

    // Ảnh: dán/kéo thả/tab "Upload" trong hộp thoại ảnh đều đi qua endpoint của MediaController.
    automatic_uploads: true,
    paste_data_images: true,
    images_upload_handler: function (blobInfo) {
      return upload('/admin/media/uploadimage', blobInfo.blob(), null, blobInfo.filename()).then(function (r) { return r.url; });
    },

    // Nút "browse" trong hộp thoại Link / Image mở hộp thoại chọn file ở trên.
    file_picker_types: 'file image',
    file_picker_callback: function (cb, value, meta) {
      var kind = meta.filetype === 'image' ? 'image' : 'file';
      openPicker(tinymce.activeEditor, kind, function (it) { cb(it.url, { text: it.name, title: it.name }); });
    },

    setup: function (editor) {
      editor.ui.registry.addButton('attachfile', {
        text: '📎 File',
        tooltip: msg('attach-file', 'Insert a download link (zip, Word, Excel, PDF…)'),
        onAction: function () { openPicker(editor, 'file', function (it) { insertFileLink(editor, it); }); }
      });

      editor.ui.registry.addButton('ytb', {
        text: '▶ YouTube',
        tooltip: msg('embed-youtube', 'Embed a YouTube video'),
        onAction: function () {
          editor.windowManager.open({
            title: 'YouTube',
            body: {
              type: 'panel',
              items: [{ type: 'input', name: 'url', label: msg('youtube-label', 'YouTube link'), placeholder: 'https://www.youtube.com/watch?v=…' }]
            },
            buttons: [
              { type: 'cancel', text: 'Cancel' },
              { type: 'submit', text: 'Insert', buttonType: 'primary' }
            ],
            onSubmit: function (api) {
              var v = parseYouTube(api.getData().url);
              if (!v) {
                editor.notificationManager.open({ text: msg('youtube-invalid', 'Not a valid YouTube link.'), type: 'error', timeout: 4000 });
                return;
              }
              editor.insertContent(youtubeHtml(v));
              api.close();
            }
          });
        }
      });

      editor.ui.registry.addButton('videoup', {
        text: '🎞 Video',
        tooltip: msg('upload-video', 'Upload a video file'),
        onAction: function () { document.getElementById('video-file').click(); }
      });

      editor.ui.registry.addButton('hrline', {
        text: '―',
        tooltip: msg('hr', 'Horizontal line'),
        onAction: function () { editor.insertContent('<hr />'); }
      });

      var videoInput = document.getElementById('video-file');
      if (videoInput) {
        videoInput.addEventListener('change', function () {
          var file = videoInput.files[0];
          if (!file) return;
          var note = editor.notificationManager.open({ text: msg('uploading', 'Uploading video…') + ' 0%', type: 'info', progressBar: true });
          upload('/admin/media/uploadvideo', file, function (pct) {
            note.progressBar.value(pct);
            note.text(msg('uploading', 'Uploading video…') + ' ' + pct + '%');
          }).then(function (r) {
            note.close();
            editor.insertContent('<video controls preload="metadata" playsinline src="' + r.url + '"></video><p></p>');
          }).catch(function (err) {
            note.close();
            editor.notificationManager.open({ text: String(err), type: 'error', timeout: 8000 });
          }).then(function () { videoInput.value = ''; });
        });
      }
    }
  };

  if (lang && lang !== 'en') {
    cfg.language = lang;
    cfg.language_url = '/lib/tinymce/langs/' + lang + '.js';
  }

  var tabs = document.querySelectorAll('[data-editor-mode]');

  tinymce.init(cfg).then(function (editors) {
    var ed = editors[0];
    if (!ed) return;
    var htmlMode = false;

    function setMode(mode) {
      var wantHtml = mode === 'html';
      if (wantHtml === htmlMode) return;
      if (wantHtml) {
        el.value = ed.getContent();
        ed.getContainer().style.display = 'none';
        el.style.display = 'block';
        el.focus();
      } else {
        ed.setContent(el.value);
        el.style.display = 'none';
        ed.getContainer().style.display = '';
      }
      htmlMode = wantHtml;
      tabs.forEach(function (t) {
        var on = t.getAttribute('data-editor-mode') === mode;
        t.classList.toggle('active', on);
        t.setAttribute('aria-pressed', on ? 'true' : 'false');
      });
    }

    tabs.forEach(function (t) {
      t.addEventListener('click', function () { setMode(t.getAttribute('data-editor-mode')); });
    });

    // Đang ở chế độ HTML mà bấm Lưu: lấy HTML trong ô thô, trước khi TinyMCE ghi đè textarea bằng nội dung của nó.
    if (el.form) {
      el.form.addEventListener('submit', function () {
        if (htmlMode) ed.setContent(el.value);
      }, true);
    }
  });
})();
