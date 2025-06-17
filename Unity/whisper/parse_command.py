# parse_command.py

# 매핑 테이블
unit_mapping = {
    '보병': '보병', '근접': '보병', '전사': '보병', '검사': '보병', '탱커': '보병', '근거리': '보병', '병': '보병', '무병': '보병',
    '뚜벅이': '보병', '땅개': '보병', '고기': '보병', '방패': '보병', '고기방패': '보병', '고병': '보병', '칼': '보병', '부병': '보병',
    '궁병': '궁병', '궁수': '궁병', '원거리': '궁병', '활쟁이': '궁병', '활병': '궁병',
    '화살병': '궁병', '아처': '궁병', '원딜': '궁병', '공수': '궁수', '공병': '궁수',
    '기병': '기병', '기마병': '기병', '말탄병사': '기병', '말병': '기병', '빠른유닛': '기병', '말': '기병', '기사': '기병',
    '마법사': '마법사', '법사': '마법사', '마법': '마법사', '마술사': '마법사', '요술사': '마법사',
    '지팡이': '마법사', '소서러': '마법사', '메이지': '마법사',
    '전체': '전체', '전원': '전체', '모두': '전체', '모든병력': '전체', '모든': '전체', '병력': '전체', '전부': '전체', '전부다': '전체',
    '다': '전체',
}

direction_mapping = {
    '앞': '앞', '앞으로': '앞', '전방': '앞', '돌격': '앞', '돌진': '앞',
    '뒤': '뒤', '뒤로': '뒤', '뒤쪽': '뒤', '후방': '뒤', '후면': '뒤', '후퇴': '뒤', '빼': '뒤', '떼': '뒤',
    '위': '위', '위로': '위', '위쪽': '위', '상단': '위', '위방향': '위',
    '아래': '아래', '아래로': '아래', '아래쪽': '아래', '하단': '아래', '아래방향': '아래', '밑쪽': '아래', '밑': '아래', '밑으로': '아래',
    '정지': '정지', '멈춰': '정지'
}

def parse_command_from_keywords(keywords):
    unit_words = []
    direction_word = None

    for word in keywords:
        if word in unit_mapping:
            unit_words.append(unit_mapping[word])
        elif word in direction_mapping and direction_word is None:
            direction_word = direction_mapping[word]

    # 공격 명령: 병종 2개
    if len(unit_words) >= 2 and unit_words[1] == '전체':
        return {
            "infantry": None,
            "direction": None
        }

    elif len(unit_words) >= 2:
        return {
            "infantry": unit_words[0],
            "target": unit_words[1]
        }

    # 이동 명령: 병종 1개 + 방향
    elif len(unit_words) == 1 and direction_word:
        return {
            "infantry": unit_words[0],
            "direction": direction_word
        }

    # 그 외는 무효 처리
    return {
        "infantry": None,
        "direction": None
    }
