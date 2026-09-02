import { useState } from 'react';
import type { AnalysisResult, Incident, InvestigationReport, ResponseEducationPackage } from '../api/types';

interface EducationExperienceProps {
  incident: Incident;
  responsePackage: ResponseEducationPackage;
  report: InvestigationReport | null;
  analysis: AnalysisResult | null;
}

export function EducationExperience({ incident, responsePackage, report, analysis }: EducationExperienceProps) {
  const { education } = responsePackage;
  const [openWarning, setOpenWarning] = useState<string | null>(null);
  const [reviewedEvidence, setReviewedEvidence] = useState(false);
  const [questionIndex, setQuestionIndex] = useState(0);
  const [selectedAnswer, setSelectedAnswer] = useState<number | null>(null);
  const [submitted, setSubmitted] = useState(false);
  const [correctAnswers, setCorrectAnswers] = useState(0);
  const [showResults, setShowResults] = useState(false);

  const question = education.questions[questionIndex];
  const finished = education.questions.length > 0 && showResults;
  const lessonProgress = finished ? 4 : submitted ? 3 : reviewedEvidence ? 2 : 1;

  function submitAnswer(): void {
    if (selectedAnswer === null || !question) {
      return;
    }

    setSubmitted(true);
    if (selectedAnswer === question.correctOptionIndex) {
      setCorrectAnswers((current) => current + 1);
    }
  }

  function nextQuestion(): void {
    setQuestionIndex((current) => current + 1);
    setSelectedAnswer(null);
    setSubmitted(false);
  }

  return (
    <article className="education-experience space-stack">
      <div className="lesson-header">
        <div>
          <p className="lesson-kicker">Learn From This Incident</p>
          <h2>{education.title}</h2>
          <p className="lesson-objective"><strong>Goal:</strong> {education.learningObjective}</p>
        </div>
        <div className="meta-chip-row">
          <span className="meta-chip">{education.estimatedMinutes} min</span>
          <span className="meta-chip">{education.audienceLevel}</span>
          <span className="meta-chip">{incident.technicalSkillLevel}</span>
        </div>
      </div>

      <ol className="lesson-progress" aria-label="Lesson progress">
        {['Understand the incident', 'Review warning signs', 'Check your knowledge', 'Key takeaways'].map((step, index) => (
          <li key={step} className={index < lessonProgress ? 'is-complete' : index === lessonProgress ? 'is-current' : ''}>
            <span aria-hidden="true">{index < lessonProgress ? '✓' : index + 1}</span>{step}
          </li>
        ))}
      </ol>

      <section className="lesson-section">
        <h3>What Happened</h3>
        <p>{responsePackage.plainLanguageSummary}</p>
        <ol className="incident-replay">
          {(report?.findings ?? []).slice(0, 4).map((finding) => <li key={finding.id}>{finding.title}: {finding.description}</li>)}
          {!report?.findings.length && analysis ? <li>{analysis.summary}</li> : null}
        </ol>
      </section>

      <section className="lesson-section">
        <h3>Warning Signs You Could Spot</h3>
        <div className="warning-sign-grid">
          {education.warningSigns.map((sign) => {
            const evidence = (report?.findings ?? []).filter((finding) => sign.supportingFindingIds.includes(finding.id));
            const isOpen = openWarning === sign.title;
            return (
              <article key={sign.title} className="warning-sign-card">
                <h4><span aria-hidden="true">!</span>{sign.title}</h4>
                <p>{sign.explanation}</p>
                <button type="button" className="text-button" aria-expanded={isOpen} onClick={() => { setOpenWarning(isOpen ? null : sign.title); setReviewedEvidence(true); }}>
                  {isOpen ? 'Hide evidence' : 'Show the evidence'}
                </button>
                {isOpen ? (
                  <div className="evidence-snippet">
                    {evidence.length > 0 ? evidence.map((finding) => <p key={finding.id}><strong>{finding.title}</strong><br />{finding.evidence}</p>) : <p>ARGUS linked this warning sign to validated investigation findings. No additional display-safe evidence is available.</p>}
                  </div>
                ) : null}
              </article>
            );
          })}
        </div>
      </section>

      {question && !finished ? (
        <section className="lesson-section knowledge-check">
          <p className="lesson-kicker">Knowledge Check {questionIndex + 1} of {education.questions.length}</p>
          <h3>{question.question}</h3>
          <fieldset disabled={submitted} className="quiz-options">
            <legend className="sr-only">Answer options</legend>
            {question.options.map((option, optionIndex) => (
              <label key={option} className="quiz-option">
                <input type="radio" name={question.id} checked={selectedAnswer === optionIndex} onChange={() => setSelectedAnswer(optionIndex)} />
                <span>{option}</span>
              </label>
            ))}
          </fieldset>
          {!submitted ? <button type="button" className="button-primary" disabled={selectedAnswer === null} onClick={submitAnswer}>Check Answer</button> : null}
          {submitted ? (
            <div className="quiz-feedback" role="status">
              <strong>{selectedAnswer === question.correctOptionIndex ? 'Correct.' : 'Not quite.'}</strong> {question.explanation}
              {questionIndex + 1 < education.questions.length ? <button type="button" className="button-primary" onClick={nextQuestion}>Next Question</button> : <button type="button" className="button-primary" onClick={() => setShowResults(true)}>See Results</button>}
            </div>
          ) : null}
        </section>
      ) : null}

      {finished ? (
        <section className="lesson-section quiz-complete" role="status">
          <h3>Knowledge Check Complete</h3>
          <p><strong>{correctAnswers} / {education.questions.length} correct</strong></p>
          <p>{correctAnswers === education.questions.length ? 'You identified the major warning signs in this incident.' : correctAnswers >= Math.ceil(education.questions.length / 2) ? 'You caught several warning signs. Review the highlighted evidence before finishing.' : 'Review the warning-sign section again before moving on.'}</p>
        </section>
      ) : null}

      {education.takeaways.length > 0 ? (
        <section className="lesson-section">
          <h3>Remember</h3>
          <ol className="takeaway-list">{education.takeaways.slice(0, 3).map((takeaway) => <li key={takeaway}>{takeaway}</li>)}</ol>
        </section>
      ) : null}
    </article>
  );
}