import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { put } from "../../api/client";
import type { PlanOverview } from "../../api/types";
import { ErrorText } from "../../components/Field";
import { JalaliDateInput } from "../../components/JalaliDateInput";

/** ویرایش تاریخ‌های پیش‌بینی‌شده زیربرنامه: شروع، پایان و پایان هر مرحله. */
export function ScheduleEditor({ o, onDone }: { o: PlanOverview; onDone: () => void }) {
  const qc = useQueryClient();
  const [start, setStart] = useState(o.plannedStartDate ?? "");
  const [end, setEnd] = useState(o.plannedEndDate ?? "");
  const [stages, setStages] = useState<Record<string, string>>(
    Object.fromEntries(o.stages.filter((s) => s.plannedDate).map((s) => [s.stageId, s.plannedDate!])),
  );

  const save = useMutation({
    mutationFn: (redistribute: boolean) =>
      put<PlanOverview>(`/plans/${o.id}/schedule`, {
        plannedStartDate: start,
        plannedEndDate: end,
        stages: redistribute ? null : Object.entries(stages).map(([stageId, plannedDate]) => ({ stageId, plannedDate })),
      }),
    onSuccess: (data) => {
      qc.setQueryData(["plans", o.id, "overview"], data);
      qc.invalidateQueries({ queryKey: ["reports"] });
      onDone();
    },
  });

  function submit(e: FormEvent) {
    e.preventDefault();
    save.mutate(false);
  }

  return (
    <form className="schedule-editor" onSubmit={submit}>
      <div className="grid-2">
        <label>
          شروع پیش‌بینی‌شده
          <JalaliDateInput label="شروع پیش‌بینی‌شده" value={start} onChange={setStart} required />
        </label>
        <label>
          پایان پیش‌بینی‌شده
          <JalaliDateInput label="پایان پیش‌بینی‌شده" value={end} onChange={setEnd} required />
        </label>
        {o.stages
          .filter((s) => s.stageId in stages)
          .map((s) => (
            <label key={s.stageId}>
              پایان {s.name}
              <JalaliDateInput
                label={`پایان ${s.name}`}
                value={stages[s.stageId]}
                onChange={(v) => setStages({ ...stages, [s.stageId]: v })}
                required
              />
            </label>
          ))}
      </div>
      <ErrorText error={save.error} />
      <div className="actions">
        <button type="submit" disabled={save.isPending || !start || !end}>
          ذخیره زمان‌بندی
        </button>
        <button type="button" className="secondary" disabled={save.isPending || !start || !end} onClick={() => save.mutate(true)}>
          پخش یکنواخت مراحل بین شروع و پایان
        </button>
        <button type="button" className="secondary" onClick={onDone}>
          انصراف
        </button>
      </div>
    </form>
  );
}
