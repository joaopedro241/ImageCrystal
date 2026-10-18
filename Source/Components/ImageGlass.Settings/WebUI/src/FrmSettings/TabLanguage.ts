// ImageCrystal modifications by xenon, 2026-10-18; see NOTICE.ImageCrystal.txt.
import { ILanguage } from '@/@types/FrmSettings';
import { getChangedSettingsFromTab } from '@/helpers';

export default class TabLanguage {
  /**
   * Loads settings for tab Language.
   */
  static loadSettings() {
    TabLanguage.handleLanguageChanged();
  }


  /**
   * Adds events for tab Language.
   */
  static addEvents() {
    query('#Cmb_LanguageList').addEventListener('change', TabLanguage.handleLanguageChanged, false);
    query('#Btn_RefreshLanguageList').addEventListener('click', TabLanguage.onBtn_RefreshLanguageList, false);
    query('#Lnk_InstallLanguage').addEventListener('click', TabLanguage.onLnk_InstallLanguage, false);
    query('#Lnk_ExportLanguage').addEventListener('click', () => post('Lnk_ExportLanguage', query<HTMLSelectElement>('#Cmb_LanguageList').value), false);
  }


  private static async onBtn_RefreshLanguageList() {
    const newLangList = await postAsync<ILanguage[]>('Btn_RefreshLanguageList');
    TabLanguage.loadLanguageList(newLangList);
  }


  private static async onLnk_InstallLanguage() {
    const newLangList = await postAsync<ILanguage[]>('Lnk_InstallLanguage');
    TabLanguage.loadLanguageList(newLangList);
  }


  /**
   * Save settings as JSON object.
   */
  static exportSettings() {
    return getChangedSettingsFromTab('language');
  }


  /**
   * Handles when language is changed.
   */
  private static handleLanguageChanged() {
    const langFileName = query<HTMLSelectElement>('#Cmb_LanguageList').value;
    const lang = _pageSettings.langList.find(i => i.FileName === langFileName);
    if (!lang) return;

    const languageAuthors = lang.Metadata.Author
      .replace(/Duong Dieu Phap\s*(\(original author\))?/gi, '')
      .replace(/xenon/gi, '')
      .trim().replace(/^[,;\s]+|[,;\s]+$/g, '');
    query('#Section_LanguageContributors').innerText = [
      'xenon',
      'Duong Dieu Phap (original author)',
      ...(languageAuthors ? [languageAuthors] : []),
    ].join('\n');
  }


  /**
   * Loads language list to select box.
   * @param list If defined, it overrides `_pageSettings.langList`.
   */
  static loadLanguageList(list?: ILanguage[]) {
    const selectEl = query<HTMLSelectElement>('#Cmb_LanguageList');

    // clear current list
    while (selectEl.options.length) selectEl.remove(0);

    if (Array.isArray(list) && list.length > 0) {
      _pageSettings.langList = list;
    }

    _pageSettings.langList.forEach(lang => {
      let displayText = `${lang.Metadata.LocalName} (${lang.Metadata.EnglishName})`;
      if (!lang.FileName || lang.FileName.length === 0) {
        displayText = lang.Metadata.EnglishName;
      }

      const optionEl = new Option(displayText, lang.FileName);
      selectEl.add(optionEl);
    });

    selectEl.value = _pageSettings.config.Language;
    TabLanguage.handleLanguageChanged();
  }
}
