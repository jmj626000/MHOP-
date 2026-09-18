"""AI 心理评估：标准量表计分 + AI 解读。

匿名用户：服务端不做任何持久化（隐私优先），结果返回后由前端存入浏览器本地；
登录用户：默认仍不落库，仅在显式勾选 save_to_cloud 时写入云端。
"""
import json

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import select
from sqlalchemy.orm import Session

from .. import ai
from ..database import get_db
from ..deps import get_current_user, get_user_optional
from ..moderation import detect_crisis
from ..models import Assessment, User
from ..scales import SCALES, score_band
from ..schemas import AssessmentIn, AssessmentOut

router = APIRouter(prefix="/api/assessments", tags=["assessment"])


@router.get("/scales")
def get_scales():
    return list(SCALES.values())


@router.post("", response_model=AssessmentOut)
async def submit_assessment(
    body: AssessmentIn,
    db: Session = Depends(get_db),
    current: User | None = Depends(get_user_optional),
):
    score = None
    level, level_code = "", ""
    crisis = detect_crisis(body.free_text)

    if body.assessment_type in SCALES:
        scale = SCALES[body.assessment_type]
        n = len(scale["questions"])
        answers = body.answers or {}
        try:
            values = [int(answers[str(i)]) for i in range(n)]
        except (KeyError, TypeError, ValueError):
            raise HTTPException(status_code=400, detail="请完成量表全部题目")
        if any(v not in (0, 1, 2, 3) for v in values):
            raise HTTPException(status_code=400, detail="量表作答值非法")
        score = sum(values)
        level, level_code = score_band(body.assessment_type, score)
        ci = scale["crisis_index"]
        if ci is not None and values[ci] > 0:
            crisis = True
    elif body.assessment_type != "free":
        raise HTTPException(status_code=400, detail="未知的评估类型")
    elif not body.free_text.strip():
        raise HTTPException(status_code=400, detail="请先描述你最近的状态与感受")

    result, engine = await ai.assess(body.assessment_type, score, level, body.free_text.strip(), crisis)

    saved = False
    record_id = None
    created_at = None
    if body.save_to_cloud:
        if current is None:
            raise HTTPException(status_code=401, detail="登录后才能保存到云端")
        record = Assessment(
            user_id=current.id,
            assessment_type=body.assessment_type,
            input_data=json.dumps({"answers": body.answers, "free_text": body.free_text}, ensure_ascii=False),
            ai_result=result,
            score=score,
            level=level,
        )
        db.add(record)
        db.commit()
        db.refresh(record)
        saved, record_id, created_at = True, record.id, record.created_at

    return AssessmentOut(
        id=record_id,
        assessment_type=body.assessment_type,
        score=score,
        level=level,
        level_code=level_code,
        crisis=crisis,
        ai_result=result,
        saved_cloud=saved,
        created_at=created_at,
    )


@router.get("/mine")
def my_assessments(
    db: Session = Depends(get_db),
    current: User = Depends(get_current_user),
):
    rows = db.scalars(
        select(Assessment).where(Assessment.user_id == current.id).order_by(Assessment.created_at.desc())
    ).all()
    return [
        AssessmentOut(
            id=r.id,
            assessment_type=r.assessment_type,
            score=r.score,
            level=r.level or "",
            crisis=False,
            ai_result=r.ai_result,
            saved_cloud=True,
            created_at=r.created_at,
        )
        for r in rows
    ]
