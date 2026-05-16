import { DOCUMENT } from '@angular/common';
import { Component, Input, NgZone, OnChanges, OnDestroy, SimpleChanges, inject, signal } from '@angular/core';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faFacebook, faWhatsapp, faXTwitter } from '@fortawesome/free-brands-svg-icons';
import { faCopy, faShareNodes, faTriangleExclamation } from '@fortawesome/free-solid-svg-icons';
import { environment } from '../../../environments/environment';
import { Word } from '../../models/word';
import { WordSearchService } from '../../services/word-search.service';

interface TurnstileApi {
  render(container: string | HTMLElement, options: Record<string, unknown>): string;
  reset(widgetId?: string): void;
  remove?(widgetId: string): void;
}

declare global {
  interface Window {
    turnstile?: TurnstileApi;
  }
}

type DefinitionPart =
  | { kind: 'text'; value: string }
  | { kind: 'tag'; key: string; tooltip: string; colorClass: string };

const TAG_COLOR_CLASSES = ['tag-primary', 'tag-secondary', 'tag-tertiary'] as const;
const FORCED_TAG_COLOR_CLASS_BY_KEY: Record<string, (typeof TAG_COLOR_CLASSES)[number]> = {
  'س': 'tag-primary'
};

const TAG_TOOLTIP_BY_KEY: Record<string, string> = {
  'أر': 'أوروبية',
  'إغ': 'إغريقية',
  'اهـ': 'انتهى النص',
  'بج': 'بجاوية',
  'بد': 'بدري والإشارة إلى كتاب الأمثال السودانية للشيخ بابكر بدري والرقم يشير إلى رقم المثل لا الصفحة .',
  'بدر': 'كتاب « اللغة النوبية » لمحمد متولي بدر القاهرة 1955 .',
  'بنعَبْد': 'عبد العزيز بنعبد الله في معجمه عن العامية المغربية .',
  'تر': 'تركية',
  'ج': 'جمع',
  'د': 'دخيلة',
  'دن': 'دنقلاوية',
  'س': 'عامية سودانية',
  'سر': 'سريانية',
  'ش': 'شامية',
  // 'ص': 'صلى الله عليه وسلم',
  'طبقات': 'طبقات ود ضيف الله',
  'طل': 'طلبانية',
  'ع': 'عاميات عربية',
  'عب': 'عبد المجيد عابدين : من أصول اللهجات العربية وتاريخ الثقافة ودراسات سودانية ، ومن روايته لي شخصياً .',
  'عرح': 'عربية حديثة',
  'عس': 'العربية في السودان للشيخ عبد الله عبد الرحمن .',
  'غرائب': 'غرائب اللهجة اللبنانية السورية لروفائيل نخلة',
  'ف': 'فصيحة',
  'فر': 'فارسية',
  'ق': 'قبطية',
  'م': 'انظر هذه المادة في موضعها',
  'مدونات': 'مجلة السودان في رسائل ومدونات SNR',
  'مص': 'مصرية',
  'مغ': 'مغربية',
  'مو': 'مولدة',
  'ن': 'نوبية',
  Hava: 'قاموس الفرائد الدرية للأب حوّا : هافا .',
  'S. A.': 'Hillelson: Sudan Arabic',
  '؟': 'المصدر أو الأصل غير معروف .',
  "و'": 'تنطق الواو ممالة في مثل اللفظة الانجليزية boy .',
  "ي'": 'تنطق الياء ممالة في مثل اللفظة الانجليزية day .'
};

const TAG_KEYS_SORTED = Object.keys(TAG_TOOLTIP_BY_KEY).sort((first, second) => second.length - first.length);

@Component({
  selector: 'app-word-card',
  imports: [FontAwesomeModule],
  templateUrl: './word-card.component.html',
  styleUrl: './word-card.component.css'
})
export class WordCardComponent implements OnChanges, OnDestroy {
  private readonly document = inject(DOCUMENT);
  private readonly ngZone = inject(NgZone);
  private readonly wordSearchService = inject(WordSearchService);

  @Input({ required: true }) word!: Word;

  protected definitionParts: DefinitionPart[] = [];
  protected isSharePopupOpen = false;
  protected isFeedbackPopupOpen = false;
  protected readonly copyFeedback = signal('');
  protected readonly feedbackText = signal('');
  protected readonly feedbackError = signal('');
  protected readonly feedbackSuccess = signal('');
  protected readonly isSubmittingFeedback = signal(false);
  protected readonly shareIcon = faShareNodes;
  protected readonly reportIcon = faTriangleExclamation;
  protected readonly copyIcon = faCopy;
  protected readonly facebookIcon = faFacebook;
  protected readonly xTwitterIcon = faXTwitter;
  protected readonly whatsappIcon = faWhatsapp;
  protected readonly feedbackCaptchaContainerId = `feedback-turnstile-${Math.random().toString(36).slice(2)}`;
  protected readonly isTurnstileConfigured = Boolean(environment.turnstileSiteKey);
  private feedbackTimeout: number | undefined;
  private turnstileLoadPromise: Promise<boolean> | null = null;
  private turnstileWidgetId: string | null = null;
  private turnstileToken: string | null = null;

  ngOnDestroy(): void {
    this.clearFeedback();
    this.closeFeedbackPopup();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['word']) {
      this.definitionParts = this.parseDefinition(this.word.definition);
      this.isSharePopupOpen = false;
      this.isFeedbackPopupOpen = false;
      this.clearFeedback();
      this.resetFeedbackForm();
    }
  }

  protected openSharePopup(): void {
    this.closeFeedbackPopup();
    this.isSharePopupOpen = true;
  }

  protected closeSharePopup(): void {
    this.isSharePopupOpen = false;
  }

  protected openFeedbackPopup(): void {
    this.closeSharePopup();
    this.resetFeedbackForm();
    this.isFeedbackPopupOpen = true;
    void this.initializeTurnstileWidget();
  }

  protected closeFeedbackPopup(): void {
    this.isFeedbackPopupOpen = false;
    this.turnstileToken = null;

    const turnstile = this.document.defaultView?.turnstile;
    if (turnstile && this.turnstileWidgetId !== null) {
      if (turnstile.remove) {
        turnstile.remove(this.turnstileWidgetId);
      } else {
        turnstile.reset(this.turnstileWidgetId);
      }
    }

    this.turnstileWidgetId = null;
  }

  protected onFeedbackInput(event: Event): void {
    const target = event.target as HTMLTextAreaElement;
    this.feedbackText.set(target.value);
  }

  protected submitFeedback(): void {
    this.feedbackError.set('');
    this.feedbackSuccess.set('');

    const normalizedText = this.feedbackText().trim();
    if (!normalizedText) {
      this.feedbackError.set('يرجى كتابة تفاصيل الخطأ.');
      return;
    }

    if (!this.turnstileToken) {
      this.feedbackError.set('يرجى إكمال التحقق الأمني أولاً.');
      return;
    }

    this.isSubmittingFeedback.set(true);

    this.wordSearchService.submitFeedback(this.word.id, normalizedText, this.turnstileToken).subscribe({
      next: () => {
        this.feedbackSuccess.set('تم إرسال البلاغ بنجاح. شكراً لمساعدتك.');
        this.feedbackText.set('');
        this.resetTurnstileWidget();
        this.isSubmittingFeedback.set(false);
      },
      error: (error: unknown) => {
        this.feedbackError.set(this.extractFeedbackErrorMessage(error));
        this.resetTurnstileWidget();
        this.isSubmittingFeedback.set(false);
      }
    });
  }

  protected get shareUrl(): string {
    return this.buildShareUrl();
  }

  protected copyShareLink(): void {
    this.clearFeedback();
    void this.copyText(this.shareUrl)
      .then(() => {
        this.ngZone.run(() => {
          this.copyFeedback.set('تم نسخ الرابط.');
          this.scheduleFeedbackClear();
        });
      })
      .catch(() => {
        this.ngZone.run(() => {
          this.copyFeedback.set('تعذر نسخ الرابط تلقائياً.');
          this.scheduleFeedbackClear();
        });
      });
  }

  protected shareOnFacebook(): void {
    const url = `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(this.shareUrl)}`;
    this.openShareWindow(url);
  }

  protected shareOnTwitter(): void {
    const text = this.getShareTemplate();
    const url = `https://twitter.com/intent/tweet?url=${encodeURIComponent(this.shareUrl)}&text=${encodeURIComponent(text)}`;
    this.openShareWindow(url);
  }

  protected shareOnWhatsapp(): void {
    const text = `${this.getShareTemplate()} ${this.shareUrl}`;
    const url = `https://wa.me/?text=${encodeURIComponent(text)}`;
    this.openShareWindow(url);
  }

  private parseDefinition(definition: string): DefinitionPart[] {
    const parts: DefinitionPart[] = [];
    const regex = /\(([^()]+)\)/g;

    let lastIndex = 0;
    let match: RegExpExecArray | null;

    while ((match = regex.exec(definition)) !== null) {
      const fullMatch = match[0];
      const rawContent = match[1];
      const matchStart = match.index;
      const matchEnd = matchStart + fullMatch.length;

      if (matchStart > lastIndex) {
        parts.push({ kind: 'text', value: definition.slice(lastIndex, matchStart) });
      }

      const parsedContent = this.parseParenthesizedContent(rawContent);
      if (parsedContent.hasRecognizedTag) {
        parts.push({ kind: 'text', value: '(' });
        parts.push(...parsedContent.parts);
        parts.push({ kind: 'text', value: ')' });
      } else {
        parts.push({ kind: 'text', value: fullMatch });
      }

      lastIndex = matchEnd;
    }

    if (lastIndex < definition.length) {
      parts.push({ kind: 'text', value: definition.slice(lastIndex) });
    }

    if (parts.length === 0) {
      parts.push({ kind: 'text', value: definition });
    }

    return parts;
  }

  private parseParenthesizedContent(content: string): { parts: DefinitionPart[]; hasRecognizedTag: boolean } {
    const parts: DefinitionPart[] = [];
    let cursor = 0;
    let hasRecognizedTag = false;
    let recognizedTagCount = 0;

    while (cursor < content.length) {
      const matchedKey = this.findMatchingKey(content, cursor);
      if (matchedKey) {
        const forcedColorClass = FORCED_TAG_COLOR_CLASS_BY_KEY[matchedKey];
        const colorClass = forcedColorClass ?? TAG_COLOR_CLASSES[recognizedTagCount % TAG_COLOR_CLASSES.length];
        parts.push({
          kind: 'tag',
          key: matchedKey,
          tooltip: TAG_TOOLTIP_BY_KEY[matchedKey],
          colorClass
        });
        cursor += matchedKey.length;
        hasRecognizedTag = true;
        if (!forcedColorClass) {
          recognizedTagCount += 1;
        }
        continue;
      }

      const nextTagIndex = this.findNextTagStart(content, cursor + 1);
      const end = nextTagIndex === -1 ? content.length : nextTagIndex;
      parts.push({ kind: 'text', value: content.slice(cursor, end) });
      cursor = end;
    }

    return { parts, hasRecognizedTag };
  }

  private findNextTagStart(content: string, startIndex: number): number {
    for (let index = startIndex; index < content.length; index += 1) {
      if (this.findMatchingKey(content, index)) {
        return index;
      }
    }

    return -1;
  }

  private findMatchingKey(content: string, index: number): string | null {
    for (const key of TAG_KEYS_SORTED) {
      if (!content.startsWith(key, index)) {
        continue;
      }

      const beforeChar = index > 0 ? content[index - 1] : '';
      const afterIndex = index + key.length;
      const afterChar = afterIndex < content.length ? content[afterIndex] : '';

      if (this.isBoundary(beforeChar) && this.isBoundary(afterChar)) {
        return key;
      }
    }

    return null;
  }

  private isBoundary(char: string): boolean {
    if (!char) {
      return true;
    }

    return /[\s،,;؛/|+.-]/.test(char);
  }

  private getShareTemplate(): string {
    return `معنى كلمة ${this.word.headword} في قاموس اللهجة السودانية`;
  }

  private buildShareUrl(): string {
    const baseUrl = this.document.baseURI;
    return new URL(`word/${this.word.id}`, baseUrl).toString();
  }

  private openShareWindow(url: string): void {
    const defaultView = this.document.defaultView;
    if (!defaultView) {
      return;
    }

    defaultView.open(url, '_blank', 'noopener,noreferrer');
  }

  private clearFeedback(): void {
    if (this.feedbackTimeout !== undefined) {
      clearTimeout(this.feedbackTimeout);
    }
    this.copyFeedback.set('');
  }

  private scheduleFeedbackClear(): void {
    if (this.feedbackTimeout !== undefined) {
      clearTimeout(this.feedbackTimeout);
    }
    this.feedbackTimeout = window.setTimeout(() => {
      this.copyFeedback.set('');
    }, 4500);
  }

  private async copyText(value: string): Promise<void> {
    const clipboard = this.document.defaultView?.navigator.clipboard;
    if (clipboard?.writeText) {
      await clipboard.writeText(value);
      return;
    }

    const textarea = this.document.createElement('textarea');
    textarea.value = value;
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    this.document.body.appendChild(textarea);
    textarea.focus();
    textarea.select();
    this.document.execCommand('copy');
    this.document.body.removeChild(textarea);
  }

  private async initializeTurnstileWidget(): Promise<void> {
    if (!this.isFeedbackPopupOpen) {
      return;
    }

    if (!environment.turnstileSiteKey) {
      this.feedbackError.set('خدمة التحقق غير مفعلة حالياً.');
      return;
    }

    const loaded = await this.ensureTurnstileLoaded();
    if (!loaded || !this.isFeedbackPopupOpen) {
      this.feedbackError.set('تعذر تحميل التحقق الأمني. حاول مرة أخرى.');
      return;
    }

    const turnstile = this.document.defaultView?.turnstile;
    const container = await this.waitForCaptchaContainer();
    if (!turnstile || !container) {
      this.feedbackError.set('تعذر تحميل التحقق الأمني.');
      return;
    }

    if (this.turnstileWidgetId !== null) {
      if (turnstile.remove) {
        turnstile.remove(this.turnstileWidgetId);
      } else {
        turnstile.reset(this.turnstileWidgetId);
      }
      this.turnstileWidgetId = null;
    }

    container.innerHTML = '';

    this.turnstileWidgetId = turnstile.render(container, {
      sitekey: environment.turnstileSiteKey,
      callback: (token: string) => {
        this.ngZone.run(() => {
          this.turnstileToken = token;
          this.feedbackError.set('');
        });
      },
      'expired-callback': () => {
        this.ngZone.run(() => {
          this.turnstileToken = null;
        });
      },
      'error-callback': () => {
        this.ngZone.run(() => {
          this.turnstileToken = null;
          this.feedbackError.set('فشل التحقق الأمني. أعد المحاولة.');
        });
      }
    });
  }

  private ensureTurnstileLoaded(): Promise<boolean> {
    if (this.document.defaultView?.turnstile) {
      return Promise.resolve(true);
    }

    if (this.turnstileLoadPromise) {
      return this.turnstileLoadPromise;
    }

    this.turnstileLoadPromise = new Promise<boolean>((resolve) => {
      const scriptId = 'cloudflare-turnstile-script';
      const existingScript = this.document.getElementById(scriptId) as HTMLScriptElement | null;

      const resolveFromWindow = (): void => resolve(Boolean(this.document.defaultView?.turnstile));

      if (existingScript) {
        if (this.document.defaultView?.turnstile) {
          resolve(true);
          return;
        }

        existingScript.addEventListener('load', resolveFromWindow, { once: true });
        existingScript.addEventListener('error', () => resolve(false), { once: true });

        this.document.defaultView?.setTimeout(() => {
          resolve(Boolean(this.document.defaultView?.turnstile));
        }, 3000);

        return;
      }

      const script = this.document.createElement('script');
      script.id = scriptId;
      script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
      script.async = true;
      script.defer = true;
      script.onload = resolveFromWindow;
      script.onerror = () => resolve(false);
      this.document.head.appendChild(script);
    });

    return this.turnstileLoadPromise;
  }

  private resetTurnstileWidget(): void {
    this.turnstileToken = null;
    if (this.turnstileWidgetId === null) {
      return;
    }

    this.document.defaultView?.turnstile?.reset(this.turnstileWidgetId);
  }

  private resetFeedbackForm(): void {
    this.feedbackText.set('');
    this.feedbackError.set('');
    this.feedbackSuccess.set('');
    this.isSubmittingFeedback.set(false);
    this.turnstileToken = null;
  }

  private extractFeedbackErrorMessage(error: unknown): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const payload = (error as { error?: { error?: string } }).error;
      if (payload && typeof payload.error === 'string' && payload.error.trim()) {
        return payload.error;
      }
    }

    return 'تعذر إرسال البلاغ حالياً. حاول مرة أخرى.';
  }

  private async waitForCaptchaContainer(maxAttempts = 12): Promise<HTMLElement | null> {
    for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
      const container = this.document.getElementById(this.feedbackCaptchaContainerId);
      if (container instanceof HTMLElement) {
        return container;
      }

      await this.delay(16);
    }

    return null;
  }

  private delay(milliseconds: number): Promise<void> {
    return new Promise((resolve) => {
      const defaultView = this.document.defaultView;
      if (defaultView) {
        defaultView.setTimeout(resolve, milliseconds);
        return;
      }

      resolve();
    });
  }
}
