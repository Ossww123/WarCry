// src/utils/dateUtils.js
import { format, parseISO } from 'date-fns';

/**
 * ISO 문자열 날짜를 포맷팅하여 반환
 * @param {string} isoString - ISO 형식 날짜 문자열 (예: "2025-05-18T14:30:00Z")
 * @param {string} formatStr - 포맷 패턴 (예: "yyyy.MM.dd HH:mm")
 * @returns {string} - 포맷팅된 날짜 문자열
 */
export const formatDate = (isoString, formatStr = 'yyyy.MM.dd HH:mm') => {
  try {
    return format(parseISO(isoString), formatStr);
  } catch (error) {
    console.error('Invalid date format:', error);
    return isoString;
  }
};

/**
 * 날짜를 YYYYMMDD 형식으로 변환
 * @param {Date} date - Date 객체
 * @returns {string} - YYYYMMDD 형식 문자열
 */
export const toYYYYMMDD = (date) => {
  return format(date, 'yyyyMMdd');
};

/**
 * YYYYMMDD 형식 문자열을 Date 객체로 변환
 * @param {string} dateStr - YYYYMMDD 형식 문자열
 * @returns {Date} - Date 객체
 */
export const fromYYYYMMDD = (dateStr) => {
  const year = dateStr.substring(0, 4);
  const month = dateStr.substring(4, 6);
  const day = dateStr.substring(6, 8);
  return new Date(`${year}-${month}-${day}T00:00:00Z`);
};