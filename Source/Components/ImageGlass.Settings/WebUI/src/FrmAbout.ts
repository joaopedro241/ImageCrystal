// ImageCrystal modifications by xenon, 2026-10-18; see NOTICE.ImageCrystal.txt.
import './main';


const onButtonClicked = (e: Event) => {
  e.preventDefault();
  e.stopPropagation();
  const btnEl = e.target as HTMLButtonElement;

  post(btnEl.id);
};

query('#BtnClose').addEventListener('click', onButtonClicked, false);
queryAll('[data-license]').forEach(el => {
  el.addEventListener('click', () => post('ViewLicenseFile', el.getAttribute('data-license')), false);
});



window._page.loadData = (data: Record<string, string> = {}) => {
  console.info('🔵 Loading data', data);

  query('#Lbl_CopyrightsYear').innerText = `2010-${new Date().getUTCFullYear()}`;

  query('#Img_AppLogo').setAttribute('src', data.AppLogo || '');
  query('#Img_AppLogo').toggleAttribute('hidden', false);
  query('#Img_AppLogo').addEventListener('click', async () => {
    const isNotEnabled = !query('#app').style.filter;
    query('#app').style.filter = isNotEnabled ? 'url("#effectWaving")' : '';
  });

  query('#Lbl_AppCode').innerText = data.AppCode || '';
  query('#Lbl_AppVersion').innerText = data.AppVersion || '';
  query('#Lbl_AppArchitecture').innerText = data.AppArchitecture || '';
  query('#Lbl_AppRuntime').innerText = data.AppRuntime || '';
  query('#Lbl_MagickNET').innerText = data.MagickNET || '';
  query('#Lbl_WebView2Runtime').innerText = data.WebView2Runtime || '';
};


query('#BtnClose').focus();
