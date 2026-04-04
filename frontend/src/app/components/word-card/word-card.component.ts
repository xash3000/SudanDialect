import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { WordSearchResult } from '../../models/word-search-result';

type DefinitionPart =
  | { kind: 'text'; value: string }
  | { kind: 'tag'; key: string; tooltip: string };

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
  'ص': 'صلى الله عليه وسلم',
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
  templateUrl: './word-card.component.html',
  styleUrl: './word-card.component.css'
})
export class WordCardComponent implements OnChanges {
  @Input({ required: true }) word!: WordSearchResult;

  protected definitionParts: DefinitionPart[] = [];

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['word']) {
      this.definitionParts = this.parseDefinition(this.word.definition);
    }
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

    while (cursor < content.length) {
      const matchedKey = this.findMatchingKey(content, cursor);
      if (matchedKey) {
        parts.push({
          kind: 'tag',
          key: matchedKey,
          tooltip: TAG_TOOLTIP_BY_KEY[matchedKey]
        });
        cursor += matchedKey.length;
        hasRecognizedTag = true;
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
}
